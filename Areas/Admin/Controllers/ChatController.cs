using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Hubs;
using QuanLyCanTeenHutech.Models;
using QuanLyCanTeenHutech.Services;
using QuanLyCanTeenHutech.ViewModels;

namespace QuanLyCanTeenHutech.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Employee")]
public class ChatController : Controller
{
    private readonly ChatStoreService _chatStore;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatController(
        ChatStoreService chatStore,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment,
        IHubContext<ChatHub> hubContext)
    {
        _chatStore = chatStore;
        _userManager = userManager;
        _environment = environment;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var model = await BuildChatIndexViewModelAsync(currentUser);
        ViewData["Title"] = "Chat realtime";
        ViewData["ActivePage"] = "Chat";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> History(string roomType, int? conversationId, string? userId)
    {
        if (roomType == ChatRoomTypes.General)
        {
            var general = await _chatStore.GetMessagesAsync(ChatConversationHelper.GeneralConversationKey, includeDeleted: true);
            return Json(general.Select(ToViewMessage));
        }

        if (roomType == ChatRoomTypes.Support && conversationId.HasValue)
        {
            var conversation = await _chatStore.GetSupportConversationByIdAsync(conversationId.Value);
            if (conversation == null) return Json(Array.Empty<ChatMessageViewModel>());
            var support = await _chatStore.GetMessagesAsync(conversation.ConversationKey);
            return Json(support.Select(ToViewMessage));
        }

        if (roomType == ChatRoomTypes.Private && !string.IsNullOrWhiteSpace(userId))
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null) return Challenge();
            if (string.Equals(userId, admin.Id, StringComparison.Ordinal))
                return BadRequest("Không thể mở cuộc trò chuyện với chính mình.");
            var target = await _userManager.FindByIdAsync(userId);
            if (target == null) return Json(Array.Empty<ChatMessageViewModel>());
            var conversationKey = ChatConversationHelper.PrivateConversationKey(admin.Id, target.Id);
            var messages = await _chatStore.GetMessagesAsync(conversationKey);
            return Json(messages.Select(ToViewMessage));
        }

        return BadRequest("Không tìm thấy cuộc trò chuyện.");
    }

    [HttpGet]
    public async Task<IActionResult> SupportConversations(string status = ChatSupportStatus.Open)
    {
        var data = await _chatStore.GetSupportConversationsAsync(status);
        return Json(data.Select(ToSupportConversationPayload));
    }

    [HttpGet]
    public async Task<IActionResult> ArchivedPrivateConversations()
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();
        var data = await _chatStore.GetArchivedPrivateConversationsAsync(admin.Id);
        return Json(data.Select(x => new
        {
            conversationKey = x.ConversationKey,
            otherUserId = x.OtherUserId,
            otherUserName = x.OtherUserName,
            lastMessage = x.LastMessage,
            lastMessageAtText = x.LastMessageAt.HasValue ? x.LastMessageAt.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchivePrivate(string userId)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();
        if (string.Equals(userId, admin.Id, StringComparison.Ordinal))
            return BadRequest(new { success = false, message = "Không thể archive cuộc trò chuyện với chính mình." });
        var target = await _userManager.FindByIdAsync(userId);
        if (target == null) return Json(new { success = false, message = "Không tìm thấy user." });

        var conversationKey = ChatConversationHelper.PrivateConversationKey(admin.Id, target.Id);
        await _chatStore.SetPrivateArchiveAsync(conversationKey, admin.Id, target.Id, true);
        var payload = new { conversationKey, otherUserId = target.Id, otherUserName = GetDisplayName(target), adminId = admin.Id, adminName = GetDisplayName(admin), archived = true };
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(admin.Id)).SendAsync("AdminPrivateArchived", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(target.Id)).SendAsync("PrivateConversationArchived", payload);
        return Json(new { success = true, conversation = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnarchivePrivate(string userId)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();
        if (string.Equals(userId, admin.Id, StringComparison.Ordinal))
            return BadRequest(new { success = false, message = "Không thể mở cuộc trò chuyện với chính mình." });
        var target = await _userManager.FindByIdAsync(userId);
        if (target == null) return Json(new { success = false, message = "Không tìm thấy user." });

        var conversationKey = ChatConversationHelper.PrivateConversationKey(admin.Id, target.Id);
        await _chatStore.SetPrivateArchiveAsync(conversationKey, admin.Id, target.Id, false);
        var payload = new { conversationKey, otherUserId = target.Id, otherUserName = GetDisplayName(target), adminId = admin.Id, adminName = GetDisplayName(admin), archived = false };
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(admin.Id)).SendAsync("AdminPrivateArchived", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(target.Id)).SendAsync("PrivateConversationArchived", payload);
        return Json(new { success = true, conversation = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePrivate(string userId)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();
        if (string.Equals(userId, admin.Id, StringComparison.Ordinal))
            return BadRequest(new { success = false, message = "Không thể xóa cuộc trò chuyện với chính mình." });
        var target = await _userManager.FindByIdAsync(userId);
        if (target == null) return Json(new { success = false, message = "Không tìm thấy user." });

        var conversationKey = ChatConversationHelper.PrivateConversationKey(admin.Id, target.Id);
        await _chatStore.DeletePrivateConversationAsync(conversationKey);
        var payload = new { conversationKey, otherUserId = target.Id, otherUserName = GetDisplayName(target), adminId = admin.Id, adminName = GetDisplayName(admin) };
        await _hubContext.Clients.Group(conversationKey).SendAsync("PrivateConversationDeleted", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(admin.Id)).SendAsync("PrivateConversationDeleted", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(target.Id)).SendAsync("PrivateConversationDeleted", payload);
        return Json(new { success = true, conversation = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSupport(int conversationId)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var ok = await _chatStore.UpdateSupportConversationStatusAsync(conversationId, ChatSupportStatus.Closed, admin.Id);
        if (ok) await NotifySupportStatusChangedAsync(conversationId, ChatSupportStatus.Closed);
        return Json(new { success = ok });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveSupport(int conversationId)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var ok = await _chatStore.UpdateSupportConversationStatusAsync(conversationId, ChatSupportStatus.Archived, admin.Id);
        if (ok) await NotifySupportStatusChangedAsync(conversationId, ChatSupportStatus.Archived);
        return Json(new { success = ok });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSupport(int conversationId)
    {
        var conversation = await _chatStore.RemoveSupportConversationAsync(conversationId);
        if (conversation == null) return Json(new { success = false });

        var payload = new
        {
            id = conversation.Id,
            customerId = conversation.CustomerId,
            customerName = conversation.CustomerName,
            conversationKey = conversation.ConversationKey,
            status = ChatSupportStatus.Removed,
            hardRemoved = true,
            lastMessage = string.Empty,
            lastMessageAtText = string.Empty
        };

        await _hubContext.Clients.Group(ChatConversationHelper.AdminSupportGroup).SendAsync("SupportConversationStatusChanged", payload);
        await _hubContext.Clients.Group(conversation.ConversationKey).SendAsync("SupportConversationStatusChanged", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(conversation.CustomerId)).SendAsync("SupportConversationStatusChanged", payload);
        await _hubContext.Clients.Group(conversation.ConversationKey).SendAsync("SupportMessagesPurged", payload);

        return Json(new { success = true, conversation = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenSupport(int conversationId)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var ok = await _chatStore.UpdateSupportConversationStatusAsync(conversationId, ChatSupportStatus.Open, admin.Id);
        if (ok) await NotifySupportStatusChangedAsync(conversationId, ChatSupportStatus.Open);
        return Json(new { success = ok });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGeneralMessage(int messageId)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var ok = await _chatStore.DeleteGeneralMessageAsync(messageId, admin.Id);
        if (ok)
        {
            await _hubContext.Clients.Group(ChatConversationHelper.GeneralConversationKey).SendAsync("MessageDeleted", new { messageId });
        }

        return Json(new { success = ok });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleGlobalLock(bool locked)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        await _chatStore.SetGlobalLockedAsync(locked, admin.Id);
        await _hubContext.Clients.Group(ChatConversationHelper.GeneralConversationKey).SendAsync("GeneralLockedChanged", new { locked });
        return Json(new { success = true, locked });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestrictUser(string userId, string restrictionType, int? minutes, string? reason)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin == null) return Challenge();

        var target = await _userManager.FindByIdAsync(userId);
        if (target == null) return BadRequest(new { success = false, message = "Không tìm thấy user." });

        if (await IsAdminOrEmployeeAsync(target))
            return BadRequest(new { success = false, message = "Không được mute/ban Admin/Nhân viên." });

        if (restrictionType != ChatRestrictionTypes.Muted && restrictionType != ChatRestrictionTypes.Banned)
            return BadRequest(new { success = false, message = "Loại khóa không hợp lệ." });

        if (restrictionType == ChatRestrictionTypes.Muted && (!minutes.HasValue || minutes.Value <= 0)) minutes = 10;
        if (restrictionType == ChatRestrictionTypes.Muted) minutes = Math.Min(minutes!.Value, 7 * 24 * 60);
        if (restrictionType == ChatRestrictionTypes.Banned) minutes = null;
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason?.Length > 300) reason = reason[..300];

        await _chatStore.AddRestrictionAsync(userId, restrictionType, minutes, reason, admin.Id);
        var restrictionPayload = new
        {
            userId,
            userName = GetDisplayName(target),
            restrictionType,
            minutes,
            reason = reason ?? string.Empty,
            roleText = restrictionType == ChatRestrictionTypes.Banned ? "Banned chat" : "Muted chat"
        };

        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(userId)).SendAsync("ChatRestricted", restrictionPayload);
        await _hubContext.Clients.Group(ChatConversationHelper.AdminSupportGroup).SendAsync("ChatRestrictionChanged", restrictionPayload);

        return Json(new { success = true, restriction = restrictionPayload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnrestrictUser(string userId)
    {
        var target = await _userManager.FindByIdAsync(userId);
        if (target == null)
            return BadRequest(new { success = false, message = "Không tìm thấy user." });
        if (await IsAdminOrEmployeeAsync(target))
            return BadRequest(new { success = false, message = "Không thể thay đổi hạn chế của Admin/Nhân viên." });

        await _chatStore.RemoveRestrictionsAsync(userId);
        var payload = new
        {
            userId,
            userName = GetDisplayName(target),
            restrictionType = string.Empty,
            roleText = "Member"
        };
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(userId)).SendAsync("ChatUnrestricted", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.AdminSupportGroup).SendAsync("ChatRestrictionChanged", payload);
        return Json(new { success = true, restriction = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        return await SaveUploadAsync(file);
    }

    private async Task NotifySupportStatusChangedAsync(int conversationId, string status)
    {
        var conversation = await _chatStore.GetSupportConversationByIdAsync(conversationId);
        if (conversation == null) return;
        var payload = ToSupportConversationPayload(conversation);

        await _hubContext.Clients.Group(ChatConversationHelper.AdminSupportGroup).SendAsync("SupportConversationStatusChanged", payload);
        await _hubContext.Clients.Group(conversation.ConversationKey).SendAsync("SupportConversationStatusChanged", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(conversation.CustomerId)).SendAsync("SupportConversationStatusChanged", payload);
    }

    private async Task<ChatIndexViewModel> BuildChatIndexViewModelAsync(IdentityUser currentUser)
    {
        var model = new ChatIndexViewModel
        {
            CurrentUserId = currentUser.Id,
            CurrentUserName = GetDisplayName(currentUser),
            IsAdminOrEmployee = true,
            GeneralLocked = await _chatStore.IsGlobalLockedAsync(),
            SupportConversations = await _chatStore.GetSupportConversationsAsync(ChatSupportStatus.Open),
            ArchivedSupportConversations = await _chatStore.GetSupportConversationsAsync(ChatSupportStatus.Archived),
            ArchivedPrivateConversations = await _chatStore.GetArchivedPrivateConversationsAsync(currentUser.Id)
        };

        var allUsers = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        foreach (var user in allUsers.Where(u => u.Id != currentUser.Id))
        {
            var isAdminUser = await IsAdminOrEmployeeAsync(user);
            var restriction = isAdminUser ? null : await _chatStore.GetActiveRestrictionAsync(user.Id);
            var roleText = isAdminUser ? "Admin / Nhân viên" : restriction == null ? "Member" : restriction.RestrictionType == ChatRestrictionTypes.Banned ? "Banned chat" : "Muted chat";
            model.Users.Add(new ChatUserViewModel
            {
                Id = user.Id,
                Email = GetDisplayName(user),
                RoleName = roleText,
                IsAdminOrEmployee = isAdminUser
            });
        }

        return model;
    }

    private async Task<JsonResult> SaveUploadAsync(IFormFile? file)
    {
        var result = await ChatImageUploadHelper.SaveAsync(file, _environment.WebRootPath, HttpContext.RequestAborted);
        return Json(new { success = result.Success, url = result.Url, message = result.Message });
    }

    private async Task<bool> IsAdminOrEmployeeAsync(IdentityUser user)
    {
        return await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Employee");
    }

    private static string GetDisplayName(IdentityUser user)
    {
        return user.Email ?? user.UserName ?? "Người dùng";
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

    private static ChatMessageViewModel ToViewMessage(ChatMessageRecord message)
    {
        return new ChatMessageViewModel
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            ReceiverId = message.ReceiverId,
            RoomType = message.RoomType,
            ConversationKey = message.ConversationKey,
            Message = message.Message,
            ImageUrl = message.ImageUrl,
            SentAt = message.SentAt,
            IsDeleted = message.IsDeleted
        };
    }
}
