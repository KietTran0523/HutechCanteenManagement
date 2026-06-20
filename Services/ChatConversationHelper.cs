namespace QuanLyCanTeenHutech.Services;

public static class ChatConversationHelper
{
    public const string GeneralConversationKey = "general";
    public const string AdminSupportGroup = "support-admins";

    public static string UserGroup(string userId) => $"user:{userId}";

    public static string PrivateConversationKey(string userAId, string userBId)
    {
        var ids = new[] { userAId, userBId }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return $"private:{ids[0]}:{ids[1]}";
    }

    public static string NewSupportConversationKey(string customerId)
    {
        return $"support:{customerId}:{Guid.NewGuid():N}";
    }

    public static string LegacySupportConversationKey(string customerId) => $"support:{customerId}";
}
