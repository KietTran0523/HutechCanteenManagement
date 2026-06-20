namespace QuanLyCanTeenHutech.Models;

public static class ChatRoomTypes
{
    public const string General = "General";
    public const string Private = "Private";
    public const string Support = "Support";
}

public static class ChatPrivateRequestStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Disconnected = "Disconnected";
}

public static class ChatSupportStatus
{
    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Archived = "Archived";
    public const string Removed = "Removed";
}

public static class ChatRestrictionTypes
{
    public const string Muted = "Muted";
    public const string Banned = "Banned";
}

public static class ChatSettingKeys
{
    public const string GlobalLocked = "GlobalLocked";
}

public class ChatMessageRecord
{
    public int Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Người dùng";
    public string? ReceiverId { get; set; }
    public string RoomType { get; set; } = ChatRoomTypes.General;
    public string ConversationKey { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeletedById { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class ChatPrivateRequestRecord
{
    public int Id { get; set; }
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ConversationKey { get; set; } = string.Empty;
    public string Status { get; set; } = ChatPrivateRequestStatus.Pending;
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class ChatSupportConversationRecord
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ConversationKey { get; set; } = string.Empty;
    public string Status { get; set; } = ChatSupportStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public string? ClosedById { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
}


public class ChatPrivateArchiveRecord
{
    public int Id { get; set; }
    public string ConversationKey { get; set; } = string.Empty;
    public string ArchivedById { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserName { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
}

public class ChatUserRestrictionRecord
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RestrictionType { get; set; } = ChatRestrictionTypes.Muted;
    public string? Reason { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.Now;
}
