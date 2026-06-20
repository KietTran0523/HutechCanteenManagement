using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Hubs;
using QuanLyCanTeenHutech.Models;
using QuanLyCanTeenHutech.Services;
using QuanLyCanTeenHutech.ViewModels;

namespace QuanLyCanTeenHutech.Controllers;

[Authorize]
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

        if (await IsAdminOrEmployeeAsync(currentUser))
        {
            return RedirectToAction("Index", "Chat", new { area = "Admin" });
        }

        var model = await BuildChatIndexViewModelAsync(currentUser);
        ViewData["Title"] = "Chat realtime";
        ViewData["ActivePage"] = "Chat";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> History(string roomType, string? receiverId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        if (await IsAdminOrEmployeeAsync(currentUser)) return Forbid();

        var conversationKey = await ResolveConversationKeyAsync(roomType, receiverId, currentUser.Id);
        if (string.IsNullOrWhiteSpace(conversationKey)) return Json(Array.Empty<ChatMessageViewModel>());

        var messages = await _chatStore.GetMessagesAsync(conversationKey);
        return Json(messages.Select(ToViewMessage));
    }

    [HttpGet]
    public async Task<IActionResult> SupportState()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        if (await IsAdminOrEmployeeAsync(currentUser)) return Forbid();

        var conversation = await _chatStore.GetLatestVisibleSupportConversationAsync(currentUser.Id);
        return Json(conversation == null ? null : ToSupportConversationPayload(conversation));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPrivateRequest(string receiverId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        if (await IsAdminOrEmployeeAsync(currentUser)) return Forbid();

        if (string.IsNullOrWhiteSpace(receiverId) ||
            string.Equals(receiverId, currentUser.Id, StringComparison.Ordinal))
        {
            return BadRequest(new { success = false, message = "Người nhận không hợp lệ." });
        }

        var receiver = await _userManager.FindByIdAsync(receiverId);
        if (receiver == null || await IsAdminOrEmployeeAsync(receiver))
        {
            return BadRequest(new { success = false, message = "Chỉ có thể gửi request chat riêng cho member." });
        }

        var existing = await _chatStore.GetPrivateRequestAsync(currentUser.Id, receiver.Id);
        if (existing?.Status == ChatPrivateRequestStatus.Pending)
            return Conflict(new { success = false, message = "Request này đang chờ người nhận phản hồi." });
        if (existing?.Status == ChatPrivateRequestStatus.Accepted)
            return Conflict(new { success = false, message = "Hai tài khoản đã kết nối chat riêng." });

        var requestId = await _chatStore.CreatePrivateRequestAsync(currentUser.Id, receiver.Id);
        var request = await _chatStore.GetPrivateRequestByIdAsync(requestId);
        var payload = request == null ? null : ToPrivateRequestPayload(request);

        if (request != null)
        {
            await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(receiver.Id)).SendAsync("PrivateRequestCreated", payload);
            await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(currentUser.Id)).SendAsync("PrivateRequestSent", payload);
        }

        return Json(new { success = true, requestId, request = payload, message = "Đã gửi request message. Chờ người nhận accept." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptPrivateRequest(int requestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        if (await IsAdminOrEmployeeAsync(currentUser)) return Forbid();

        var ok = await _chatStore.RespondPrivateRequestAsync(requestId, currentUser.Id, ChatPrivateRequestStatus.Accepted);
        var request = ok ? await _chatStore.GetPrivateRequestByIdAsync(requestId) : null;
        var payload = request == null ? null : ToPrivateRequestPayload(request);

        if (ok && request != null)
        {
            await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(request.RequesterId)).SendAsync("PrivateRequestAccepted", payload);
            await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(request.ReceiverId)).SendAsync("PrivateRequestAccepted", payload);
        }

        return Json(new { success = ok, request = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectPrivateRequest(int requestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        if (await IsAdminOrEmployeeAsync(currentUser)) return Forbid();

        var before = await _chatStore.GetPrivateRequestByIdAsync(requestId);
        var ok = await _chatStore.RespondPrivateRequestAsync(requestId, currentUser.Id, ChatPrivateRequestStatus.Rejected);
        var request = ok ? await _chatStore.GetPrivateRequestByIdAsync(requestId) ?? before : null;
        var payload = request == null ? null : ToPrivateRequestPayload(request);

        if (ok && request != null)
        {
            await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(request.RequesterId)).SendAsync("PrivateRequestRejected", payload);
            await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(request.ReceiverId)).SendAsync("PrivateRequestRejected", payload);
        }

        return Json(new { success = ok, request = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectPrivate(string receiverId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        if (await IsAdminOrEmployeeAsync(currentUser)) return Forbid();

        var receiver = await _userManager.FindByIdAsync(receiverId);
        if (receiver == null || receiver.Id == currentUser.Id) return BadRequest(new { success = false, message = "User không hợp lệ." });
        if (await IsAdminOrEmployeeAsync(receiver)) return Json(new { success = false, message = "Admin/Nhân viên dùng Direct Message, không cần disconnect request." });

        var request = await _chatStore.DisconnectPrivateAsync(currentUser.Id, receiver.Id);
        if (request == null) return Json(new { success = false, message = "Hai tài khoản chưa connect." });

        var payload = ToPrivateRequestPayload(request);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(request.RequesterId)).SendAsync("PrivateDisconnected", payload);
        await _hubContext.Clients.Group(ChatConversationHelper.UserGroup(request.ReceiverId)).SendAsync("PrivateDisconnected", payload);

        return Json(new { success = true, request = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        if (await IsAdminOrEmployeeAsync(currentUser)) return Forbid();

        return await SaveUploadAsync(file);
    }

    private async Task<ChatIndexViewModel> BuildChatIndexViewModelAsync(IdentityUser currentUser)
    {
        var model = new ChatIndexViewModel
        {
            CurrentUserId = currentUser.Id,
            CurrentUserName = GetDisplayName(currentUser),
            IsAdminOrEmployee = false,
            GeneralLocked = await _chatStore.IsGlobalLockedAsync(),
            IncomingRequests = (await _chatStore.GetIncomingPrivateRequestsAsync(currentUser.Id))
                .Where(request => !string.Equals(request.RequesterId, currentUser.Id, StringComparison.Ordinal))
                .ToList(),
            ActiveSupportConversation = await _chatStore.GetLatestVisibleSupportConversationAsync(currentUser.Id)
        };

        var visibleAdminDirectIds = (await _chatStore.GetVisibleAdminDirectUserIdsForUserAsync(currentUser.Id)).ToHashSet();
        var allUsers = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        foreach (var user in allUsers.Where(u => u.Id != currentUser.Id))
        {
            var isAdminOrEmployee = await IsAdminOrEmployeeAsync(user);

            if (isAdminOrEmployee)
            {
                // Không hiển thị toàn bộ Admin/Nhân viên ở màn hình user.
                // Chỉ hiện Admin/Nhân viên đã từng DM với user và đoạn chat đó chưa bị archive/remove.
                if (!visibleAdminDirectIds.Contains(user.Id)) continue;

                model.Users.Add(new ChatUserViewModel
                {
                    Id = user.Id,
                    Email = GetDisplayName(user),
                    RoleName = "Admin / Nhân viên",
                    RequestStatus = "AdminDirect",
                    RequestDirection = "None",
                    IsAdminOrEmployee = true
                });
                continue;
            }

            var req = await _chatStore.GetPrivateRequestAsync(currentUser.Id, user.Id);
            model.Users.Add(new ChatUserViewModel
            {
                Id = user.Id,
                Email = GetDisplayName(user),
                RoleName = "Member",
                RequestStatus = req?.Status ?? "None",
                RequestDirection = req == null ? "None" : req.RequesterId == currentUser.Id ? "Outgoing" : "Incoming",
                RequestId = req?.Id,
                IsAdminOrEmployee = false
            });
        }

        return model;
    }

    private async Task<string?> ResolveConversationKeyAsync(string roomType, string? receiverId, string currentUserId)
    {
        if (roomType == ChatRoomTypes.General) return ChatConversationHelper.GeneralConversationKey;

        if (roomType == ChatRoomTypes.Support)
        {
            return (await _chatStore.GetLatestVisibleSupportConversationAsync(currentUserId))?.ConversationKey;
        }

        if (roomType == ChatRoomTypes.Private && !string.IsNullOrWhiteSpace(receiverId))
        {
            if (string.Equals(receiverId, currentUserId, StringComparison.Ordinal)) return null;

            var receiver = await _userManager.FindByIdAsync(receiverId);
            if (receiver == null) return null;

            var receiverIsAdminOrEmployee = await IsAdminOrEmployeeAsync(receiver);
            if (receiverIsAdminOrEmployee)
            {
                var visibleAdminIds = await _chatStore.GetVisibleAdminDirectUserIdsForUserAsync(currentUserId);
                if (!visibleAdminIds.Contains(receiverId, StringComparer.Ordinal)) return null;
            }
            else if (!await _chatStore.IsPrivateAcceptedAsync(currentUserId, receiverId))
            {
                return null;
            }

            return ChatConversationHelper.PrivateConversationKey(currentUserId, receiverId);
        }

        return null;
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

    private static object ToPrivateRequestPayload(ChatPrivateRequestRecord request)
    {
        return new
        {
            id = request.Id,
            requesterId = request.RequesterId,
            requesterName = request.RequesterName,
            receiverId = request.ReceiverId,
            receiverName = request.ReceiverName,
            conversationKey = request.ConversationKey,
            status = request.Status
        };
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
