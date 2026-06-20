using QuanLyCanTeenHutech.Models;

namespace QuanLyCanTeenHutech.ViewModels;

public class ChatIndexViewModel
{
    public string CurrentUserId { get; set; } = string.Empty;
    public string CurrentUserName { get; set; } = string.Empty;
    public bool IsAdminOrEmployee { get; set; }
    public bool GeneralLocked { get; set; }
    public List<ChatUserViewModel> Users { get; set; } = new();
    public List<ChatPrivateRequestRecord> IncomingRequests { get; set; } = new();
    public List<ChatSupportConversationRecord> SupportConversations { get; set; } = new();
    public List<ChatSupportConversationRecord> ArchivedSupportConversations { get; set; } = new();
    public List<ChatPrivateArchiveRecord> ArchivedPrivateConversations { get; set; } = new();
    public ChatSupportConversationRecord? ActiveSupportConversation { get; set; }
}

public class ChatUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string RequestStatus { get; set; } = "None";
    public string RequestDirection { get; set; } = "None";
    public int? RequestId { get; set; }
    public bool IsAdminOrEmployee { get; set; }
}

public class ChatMessageViewModel
{
    public int Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? ReceiverId { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public string ConversationKey { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsDeleted { get; set; }
    public string SentAtText => SentAt.ToString("dd/MM/yyyy HH:mm");
}
