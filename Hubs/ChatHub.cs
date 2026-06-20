using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using QuanLyCanTeenHutech.Models;
using QuanLyCanTeenHutech.Services;

namespace QuanLyCanTeenHutech.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ChatStoreService _chatStore;
    private readonly UserManager<IdentityUser> _userManager;

    public ChatHub(ChatStoreService chatStore, UserManager<IdentityUser> userManager)
    {
        _chatStore = chatStore;
        _userManager = userManager;
    }

    public override async Task OnConnectedAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ChatConversationHelper.GeneralConversationKey);
            await Groups.AddToGroupAsync(Context.ConnectionId, ChatConversationHelper.UserGroup(user.Id));

            var acceptedKeys = await _chatStore.GetAcceptedPrivateConversationKeysAsync(user.Id);
            foreach (var key in acceptedKeys)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, key);
            }

            if (await IsAdminOrEmployeeAsync(user))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ChatConversationHelper.AdminSupportGroup);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinGeneral()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ChatConversationHelper.GeneralConversationKey);
    }

    public async Task JoinPrivate(string receiverId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || string.IsNullOrWhiteSpace(receiverId) || receiverId == user.Id) return;

        var receiver = await _userManager.FindByIdAsync(receiverId);
        if (receiver == null) return;

        var canJoin = await _chatStore.IsPrivateAcceptedAsync(user.Id, receiver.Id)
                      || await IsAdminDirectAllowedAsync(user, receiver);
        if (!canJoin) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, ChatConversationHelper.PrivateConversationKey(user.Id, receiver.Id));
    }

    public async Task JoinSupport(string? conversationKey)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return;

        if (await IsAdminOrEmployeeAsync(user))
        {
            if (string.IsNullOrWhiteSpace(conversationKey)) return;
            var conversation = await _chatStore.GetSupportConversationByKeyAsync(conversationKey);
            if (conversation != null) await Groups.AddToGroupAsync(Context.ConnectionId, conversation.ConversationKey);
            return;
        }

        var active = await _chatStore.GetLatestVisibleSupportConversationAsync(user.Id);
        if (active != null) await Groups.AddToGroupAsync(Context.ConnectionId, active.ConversationKey);
    }

    public async Task SendGeneralMessage(string? message, string? imageUrl)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return;
        if (!await CanSendAsync(user)) return;

        var isAdminOrEmployee = await IsAdminOrEmployeeAsync(user);
        if (!isAdminOrEmployee && await _chatStore.IsGlobalLockedAsync())
        {
            await Clients.Caller.SendAsync("ChatError", "Chat tổng đang bị khóa. Bạn không thể gửi tin nhắn lúc này.");
            return;
        }

        var cleanMessage = NormalizeMessage(message);
        var cleanImageUrl = NormalizeImageUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(cleanMessage) && string.IsNullOrWhiteSpace(cleanImageUrl)) return;

        var chatMessage = await _chatStore.SaveMessageAsync(
            user.Id,
            null,
            ChatRoomTypes.General,
            ChatConversationHelper.GeneralConversationKey,
            cleanMessage,
            cleanImageUrl);

        var payload = ToClientMessage(chatMessage);
        await Clients.Group(ChatConversationHelper.GeneralConversationKey).SendAsync("ReceiveMessage", payload);
        await Clients.Group(ChatConversationHelper.GeneralConversationKey).SendAsync("GeneralMessageNotification", payload);
    }

    public async Task SendPrivateMessage(string receiverId, string? message, string? imageUrl)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || string.IsNullOrWhiteSpace(receiverId) || receiverId == user.Id) return;
        if (!await CanSendAsync(user)) return;

        var receiver = await _userManager.FindByIdAsync(receiverId);
        if (receiver == null) return;

        var isAdminDirect = await IsAdminDirectAllowedAsync(user, receiver);
        if (!isAdminDirect && !await _chatStore.IsPrivateAcceptedAsync(user.Id, receiver.Id))
        {
            await Clients.Caller.SendAsync("ChatError", "Hai tài khoản chưa connect hoặc đã disconnect. Hãy gửi request message lại.");
            return;
        }

        var cleanMessage = NormalizeMessage(message);
        var cleanImageUrl = NormalizeImageUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(cleanMessage) && string.IsNullOrWhiteSpace(cleanImageUrl)) return;

        var conversationKey = ChatConversationHelper.PrivateConversationKey(user.Id, receiver.Id);
        var chatMessage = await _chatStore.SaveMessageAsync(
            user.Id,
            receiver.Id,
            ChatRoomTypes.Private,
            conversationKey,
            cleanMessage,
            cleanImageUrl);

        var payload = ToClientMessage(chatMessage);
        await Clients.Group(conversationKey).SendAsync("ReceiveMessage", payload);
        await Clients.Group(ChatConversationHelper.UserGroup(user.Id)).SendAsync("ReceiveMessage", payload);
        await Clients.Group(ChatConversationHelper.UserGroup(receiver.Id)).SendAsync("ReceiveMessage", payload);
        await Clients.Group(ChatConversationHelper.UserGroup(receiver.Id)).SendAsync("PrivateMessageNotification", payload);
    }

    public async Task SendSupportMessage(string? conversationKey, string? message, string? imageUrl)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return;

        var isAdminOrEmployee = await IsAdminOrEmployeeAsync(user);
        if (!isAdminOrEmployee && !await CanSendAsync(user)) return;

        ChatSupportConversationRecord? conversation;
        if (isAdminOrEmployee)
        {
            if (string.IsNullOrWhiteSpace(conversationKey)) return;
            conversation = await _chatStore.GetSupportConversationByKeyAsync(conversationKey);
            if (conversation == null) return;
        }
        else
        {
            conversation = await _chatStore.GetOrCreateOpenSupportConversationAsync(user.Id);
            if (conversation.Status == ChatSupportStatus.Closed)
            {
                await Clients.Caller.SendAsync("ChatError", "Cuộc chat hỗ trợ này đã được Admin đóng. Bạn không thể nhắn thêm.");
                return;
            }
        }

        if (conversation.Status != ChatSupportStatus.Open)
        {
            await Clients.Caller.SendAsync("ChatError", "Cuộc chat này không còn mở. Không thể gửi tin nhắn.");
            return;
        }

        var cleanMessage = NormalizeMessage(message);
        var cleanImageUrl = NormalizeImageUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(cleanMessage) && string.IsNullOrWhiteSpace(cleanImageUrl)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, conversation.ConversationKey);
        await Clients.Caller.SendAsync("SupportConversationStarted", ToSupportConversationPayload(conversation));

        var chatMessage = await _chatStore.SaveMessageAsync(
            user.Id,
            isAdminOrEmployee ? conversation.CustomerId : null,
            ChatRoomTypes.Support,
            conversation.ConversationKey,
            cleanMessage,
            cleanImageUrl);
        await _chatStore.TouchSupportConversationAsync(conversation.Id);

        var updatedConversation = await _chatStore.GetSupportConversationByIdAsync(conversation.Id) ?? conversation;
        var payload = ToClientMessage(chatMessage);
        await Clients.Group(conversation.ConversationKey).SendAsync("ReceiveMessage", payload);
        await Clients.Group(ChatConversationHelper.AdminSupportGroup).SendAsync("SupportConversationUpdated", ToSupportConversationPayload(updatedConversation));
        await Clients.Group(ChatConversationHelper.UserGroup(conversation.CustomerId)).SendAsync("SupportMessageNotification", payload);
    }

    private async Task<bool> CanSendAsync(IdentityUser user)
    {
        var restriction = await _chatStore.GetActiveRestrictionAsync(user.Id);
        if (restriction == null) return true;

        if (restriction.RestrictionType == ChatRestrictionTypes.Banned)
        {
            await Clients.Caller.SendAsync("ChatRestricted", new { restrictionType = ChatRestrictionTypes.Banned });
            await Clients.Caller.SendAsync("ChatError", "Tài khoản của bạn đã bị cấm chat.");
            return false;
        }

        if (restriction.RestrictionType == ChatRestrictionTypes.Muted)
        {
            var endText = restriction.ExpiresAt.HasValue ? restriction.ExpiresAt.Value.ToString("dd/MM/yyyy HH:mm") : "không thời hạn";
            await Clients.Caller.SendAsync("ChatRestricted", new { restrictionType = ChatRestrictionTypes.Muted, expiresAtText = endText });
            await Clients.Caller.SendAsync("ChatError", $"Bạn đang bị mute đến {endText}.");
            return false;
        }

        return true;
    }

    private async Task<IdentityUser?> GetCurrentUserAsync()
    {
        var userName = Context.User?.Identity?.Name;
        return string.IsNullOrWhiteSpace(userName) ? null : await _userManager.FindByNameAsync(userName);
    }

    private async Task<bool> IsAdminOrEmployeeAsync(IdentityUser user)
    {
        return await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Employee");
    }

    private async Task<bool> IsAdminDirectAllowedAsync(IdentityUser sender, IdentityUser receiver)
    {
        return await IsAdminOrEmployeeAsync(sender) || await IsAdminOrEmployeeAsync(receiver);
    }

    private static string NormalizeMessage(string? message)
    {
        var cleanMessage = (message ?? string.Empty).Trim();
        return cleanMessage.Length > 1000 ? cleanMessage[..1000] : cleanMessage;
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        var clean = (imageUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean)) return null;
        if (!clean.StartsWith("/uploads/chat/", StringComparison.OrdinalIgnoreCase)) return null;
        return clean.Length > 500 ? clean[..500] : clean;
    }

    private static object ToSupportConversationPayload(ChatSupportConversationRecord conversation)
    {
        return new
        {
            id = conversation.Id,
            customerId = conversation.CustomerId,
            customerName = conversation.CustomerName,
            conversationKey = conversation.ConversationKey,
            status = conversation.Status,
            lastMessage = conversation.LastMessage,
            lastMessageAtText = conversation.LastMessageAt.ToString("dd/MM/yyyy HH:mm")
        };
    }

    private static object ToClientMessage(ChatMessageRecord message)
    {
        return new
        {
            id = message.Id,
            senderId = message.SenderId,
            senderName = message.SenderName,
            receiverId = message.ReceiverId,
            roomType = message.RoomType,
            conversationKey = message.ConversationKey,
            message = message.IsDeleted ? "Tin nhắn đã bị xóa" : message.Message,
            imageUrl = message.IsDeleted ? null : message.ImageUrl,
            isDeleted = message.IsDeleted,
            sentAtText = message.SentAt.ToString("dd/MM/yyyy HH:mm")
        };
    }
}
