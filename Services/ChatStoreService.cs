using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Data;
using QuanLyCanTeenHutech.Models;

namespace QuanLyCanTeenHutech.Services;

public class ChatStoreService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public ChatStoreService(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public static async Task EnsureSchemaAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlRawAsync(SchemaSql);
    }

    public async Task<List<ChatMessageRecord>> GetMessagesAsync(string conversationKey, int take = 100, bool includeDeleted = false)
    {
        const string sql = @"
SELECT * FROM (
    SELECT TOP (@Take)
        m.[Id], m.[SenderId], COALESCE(u.[Email], u.[UserName], N'Người dùng') AS [SenderName],
        m.[ReceiverId], m.[RoomType], m.[ConversationKey], m.[Message], m.[ImageUrl], m.[SentAt],
        m.[IsDeleted], m.[DeletedById], m.[DeletedAt]
    FROM [dbo].[ChatMessages] m
    LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = m.[SenderId]
    WHERE m.[ConversationKey] = @ConversationKey AND (@IncludeDeleted = 1 OR m.[IsDeleted] = 0)
    ORDER BY m.[SentAt] DESC, m.[Id] DESC
) x
ORDER BY x.[SentAt] ASC, x.[Id] ASC;";

        return await QueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@ConversationKey", conversationKey);
            AddParameter(cmd, "@Take", take);
            AddParameter(cmd, "@IncludeDeleted", includeDeleted ? 1 : 0);
        }, MapMessage);
    }

    public async Task<ChatMessageRecord> SaveMessageAsync(string senderId, string? receiverId, string roomType, string conversationKey, string? message, string? imageUrl)
    {
        const string sql = @"
INSERT INTO [dbo].[ChatMessages] ([SenderId], [ReceiverId], [RoomType], [ConversationKey], [Message], [ImageUrl], [SentAt], [IsRead], [IsDeleted])
OUTPUT INSERTED.[Id]
VALUES (@SenderId, @ReceiverId, @RoomType, @ConversationKey, @Message, @ImageUrl, SYSUTCDATETIME(), 0, 0);";

        var id = await ExecuteScalarAsync<int>(sql, cmd =>
        {
            AddParameter(cmd, "@SenderId", senderId);
            AddParameter(cmd, "@ReceiverId", receiverId);
            AddParameter(cmd, "@RoomType", roomType);
            AddParameter(cmd, "@ConversationKey", conversationKey);
            AddParameter(cmd, "@Message", message);
            AddParameter(cmd, "@ImageUrl", imageUrl);
        });

        return (await GetMessagesByIdAsync(id)).First();
    }

    public async Task<List<ChatMessageRecord>> GetMessagesByIdAsync(int id)
    {
        const string sql = @"
SELECT m.[Id], m.[SenderId], COALESCE(u.[Email], u.[UserName], N'Người dùng') AS [SenderName],
       m.[ReceiverId], m.[RoomType], m.[ConversationKey], m.[Message], m.[ImageUrl], m.[SentAt],
       m.[IsDeleted], m.[DeletedById], m.[DeletedAt]
FROM [dbo].[ChatMessages] m
LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = m.[SenderId]
WHERE m.[Id] = @Id;";

        return await QueryAsync(sql, cmd => AddParameter(cmd, "@Id", id), MapMessage);
    }

    public async Task<bool> DeleteGeneralMessageAsync(int messageId, string adminId)
    {
        const string sql = @"
UPDATE [dbo].[ChatMessages]
SET [IsDeleted] = 1, [DeletedById] = @AdminId, [DeletedAt] = SYSUTCDATETIME()
WHERE [Id] = @Id AND [RoomType] = N'General' AND [IsDeleted] = 0;";
        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@Id", messageId);
            AddParameter(cmd, "@AdminId", adminId);
        }) > 0;
    }

    public async Task<string?> GetPrivateRequestStatusAsync(string userAId, string userBId)
    {
        var conversationKey = ChatConversationHelper.PrivateConversationKey(userAId, userBId);
        const string sql = @"
SELECT TOP 1 [Status]
FROM [dbo].[ChatPrivateRequests]
WHERE [ConversationKey] = @ConversationKey
ORDER BY [Id] DESC;";
        return await ExecuteScalarAsync<string>(sql, cmd => AddParameter(cmd, "@ConversationKey", conversationKey));
    }

    public async Task<ChatPrivateRequestRecord?> GetPrivateRequestAsync(string userAId, string userBId)
    {
        var conversationKey = ChatConversationHelper.PrivateConversationKey(userAId, userBId);
        const string sql = @"
SELECT TOP 1 r.[Id], r.[RequesterId], COALESCE(ru.[Email], ru.[UserName], N'Người dùng') AS [RequesterName],
       r.[ReceiverId], COALESCE(vu.[Email], vu.[UserName], N'Người dùng') AS [ReceiverName],
       r.[ConversationKey], r.[Status], r.[RequestedAt], r.[RespondedAt]
FROM [dbo].[ChatPrivateRequests] r
LEFT JOIN [dbo].[AspNetUsers] ru ON ru.[Id] = r.[RequesterId]
LEFT JOIN [dbo].[AspNetUsers] vu ON vu.[Id] = r.[ReceiverId]
WHERE r.[ConversationKey] = @ConversationKey
ORDER BY r.[Id] DESC;";

        return (await QueryAsync(sql, cmd => AddParameter(cmd, "@ConversationKey", conversationKey), MapPrivateRequest)).FirstOrDefault();
    }

    public async Task<ChatPrivateRequestRecord?> GetPrivateRequestByIdAsync(int requestId)
    {
        const string sql = @"
SELECT TOP 1 r.[Id], r.[RequesterId], COALESCE(ru.[Email], ru.[UserName], N'Người dùng') AS [RequesterName],
       r.[ReceiverId], COALESCE(vu.[Email], vu.[UserName], N'Người dùng') AS [ReceiverName],
       r.[ConversationKey], r.[Status], r.[RequestedAt], r.[RespondedAt]
FROM [dbo].[ChatPrivateRequests] r
LEFT JOIN [dbo].[AspNetUsers] ru ON ru.[Id] = r.[RequesterId]
LEFT JOIN [dbo].[AspNetUsers] vu ON vu.[Id] = r.[ReceiverId]
WHERE r.[Id] = @Id;";

        return (await QueryAsync(sql, cmd => AddParameter(cmd, "@Id", requestId), MapPrivateRequest)).FirstOrDefault();
    }

    public async Task<bool> IsPrivateAcceptedAsync(string userAId, string userBId)
    {
        return string.Equals(await GetPrivateRequestStatusAsync(userAId, userBId), ChatPrivateRequestStatus.Accepted, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<int> CreatePrivateRequestAsync(string requesterId, string receiverId)
    {
        var conversationKey = ChatConversationHelper.PrivateConversationKey(requesterId, receiverId);
        var existing = await GetPrivateRequestAsync(requesterId, receiverId);
        if (existing != null && (existing.Status == ChatPrivateRequestStatus.Pending || existing.Status == ChatPrivateRequestStatus.Accepted))
        {
            return existing.Id;
        }

        const string sql = @"
INSERT INTO [dbo].[ChatPrivateRequests] ([RequesterId], [ReceiverId], [ConversationKey], [Status], [RequestedAt])
OUTPUT INSERTED.[Id]
VALUES (@RequesterId, @ReceiverId, @ConversationKey, N'Pending', SYSUTCDATETIME());";
        return await ExecuteScalarAsync<int>(sql, cmd =>
        {
            AddParameter(cmd, "@RequesterId", requesterId);
            AddParameter(cmd, "@ReceiverId", receiverId);
            AddParameter(cmd, "@ConversationKey", conversationKey);
        });
    }

    public async Task<bool> RespondPrivateRequestAsync(int requestId, string receiverId, string status)
    {
        if (status != ChatPrivateRequestStatus.Accepted && status != ChatPrivateRequestStatus.Rejected) return false;

        const string sql = @"
UPDATE [dbo].[ChatPrivateRequests]
SET [Status] = @Status, [RespondedAt] = SYSUTCDATETIME()
WHERE [Id] = @Id AND [ReceiverId] = @ReceiverId AND [Status] = N'Pending';";
        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@Id", requestId);
            AddParameter(cmd, "@ReceiverId", receiverId);
            AddParameter(cmd, "@Status", status);
        }) > 0;
    }

    public async Task<ChatPrivateRequestRecord?> DisconnectPrivateAsync(string userAId, string userBId)
    {
        var conversationKey = ChatConversationHelper.PrivateConversationKey(userAId, userBId);
        const string updateSql = @"
UPDATE [dbo].[ChatPrivateRequests]
SET [Status] = N'Disconnected', [RespondedAt] = SYSUTCDATETIME()
WHERE [Id] = (
    SELECT TOP 1 [Id]
    FROM [dbo].[ChatPrivateRequests]
    WHERE [ConversationKey] = @ConversationKey AND [Status] = N'Accepted'
    ORDER BY [Id] DESC
);";

        var ok = await ExecuteNonQueryAsync(updateSql, cmd => AddParameter(cmd, "@ConversationKey", conversationKey)) > 0;
        return ok ? await GetPrivateRequestAsync(userAId, userBId) : null;
    }

    public async Task<List<ChatPrivateRequestRecord>> GetIncomingPrivateRequestsAsync(string receiverId)
    {
        const string sql = @"
SELECT r.[Id], r.[RequesterId], COALESCE(ru.[Email], ru.[UserName], N'Người dùng') AS [RequesterName],
       r.[ReceiverId], COALESCE(vu.[Email], vu.[UserName], N'Người dùng') AS [ReceiverName],
       r.[ConversationKey], r.[Status], r.[RequestedAt], r.[RespondedAt]
FROM [dbo].[ChatPrivateRequests] r
LEFT JOIN [dbo].[AspNetUsers] ru ON ru.[Id] = r.[RequesterId]
LEFT JOIN [dbo].[AspNetUsers] vu ON vu.[Id] = r.[ReceiverId]
WHERE r.[ReceiverId] = @ReceiverId AND r.[Status] = N'Pending'
ORDER BY r.[RequestedAt] DESC;";
        return await QueryAsync(sql, cmd => AddParameter(cmd, "@ReceiverId", receiverId), MapPrivateRequest);
    }

    public async Task<List<string>> GetAcceptedPrivateConversationKeysAsync(string userId)
    {
        const string sql = @"
SELECT DISTINCT [ConversationKey]
FROM [dbo].[ChatPrivateRequests]
WHERE ([RequesterId] = @UserId OR [ReceiverId] = @UserId) AND [Status] = N'Accepted';";
        return await QueryAsync(sql, cmd => AddParameter(cmd, "@UserId", userId), r => GetString(r, "ConversationKey"));
    }

    public async Task<bool> IsPrivateArchivedForAdminAsync(string conversationKey, string adminId)
    {
        const string sql = @"
SELECT TOP 1 [IsArchived]
FROM [dbo].[ChatPrivateArchives]
WHERE [ConversationKey] = @ConversationKey AND [ArchivedById] = @AdminId
ORDER BY [Id] DESC;";
        return await ExecuteScalarAsync<bool>(sql, cmd =>
        {
            AddParameter(cmd, "@ConversationKey", conversationKey);
            AddParameter(cmd, "@AdminId", adminId);
        });
    }

    public async Task SetPrivateArchiveAsync(string conversationKey, string adminId, string otherUserId, bool archived)
    {
        const string sql = @"
MERGE [dbo].[ChatPrivateArchives] AS target
USING (SELECT @ConversationKey AS [ConversationKey], @AdminId AS [ArchivedById]) AS src
ON target.[ConversationKey] = src.[ConversationKey] AND target.[ArchivedById] = src.[ArchivedById]
WHEN MATCHED THEN
    UPDATE SET [OtherUserId] = @OtherUserId, [IsArchived] = @Archived, [ArchivedAt] = CASE WHEN @Archived = 1 THEN SYSUTCDATETIME() ELSE NULL END
WHEN NOT MATCHED THEN
    INSERT ([ConversationKey], [ArchivedById], [OtherUserId], [IsArchived], [ArchivedAt])
    VALUES (@ConversationKey, @AdminId, @OtherUserId, @Archived, CASE WHEN @Archived = 1 THEN SYSUTCDATETIME() ELSE NULL END);";
        await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@ConversationKey", conversationKey);
            AddParameter(cmd, "@AdminId", adminId);
            AddParameter(cmd, "@OtherUserId", otherUserId);
            AddParameter(cmd, "@Archived", archived ? 1 : 0);
        });
    }

    public async Task<List<ChatPrivateArchiveRecord>> GetArchivedPrivateConversationsAsync(string adminId)
    {
        const string sql = @"
SELECT a.[Id], a.[ConversationKey], a.[ArchivedById], a.[OtherUserId],
       COALESCE(u.[Email], u.[UserName], N'Người dùng') AS [OtherUserName],
       a.[IsArchived], a.[ArchivedAt],
       COALESCE(lastMsg.[Preview], N'') AS [LastMessage],
       lastMsg.[SentAt] AS [LastMessageAt]
FROM [dbo].[ChatPrivateArchives] a
LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = a.[OtherUserId]
OUTER APPLY (
    SELECT TOP 1
        CASE
            WHEN m.[IsDeleted] = 1 THEN N'Tin nhắn đã xóa'
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[Message], N''))), N'') IS NOT NULL THEN m.[Message]
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[ImageUrl], N''))), N'') IS NOT NULL THEN N'Đã gửi hình ảnh'
            ELSE N''
        END AS [Preview],
        m.[SentAt]
    FROM [dbo].[ChatMessages] m
    WHERE m.[ConversationKey] = a.[ConversationKey] AND m.[RoomType] = N'Private'
    ORDER BY m.[SentAt] DESC, m.[Id] DESC
) lastMsg
WHERE a.[ArchivedById] = @AdminId AND a.[IsArchived] = 1
ORDER BY a.[ArchivedAt] DESC, a.[Id] DESC;";
        return await QueryAsync(sql, cmd => AddParameter(cmd, "@AdminId", adminId), MapPrivateArchive);
    }


    public async Task<List<string>> GetVisibleAdminDirectUserIdsForUserAsync(string userId)
    {
        const string sql = @"
SELECT DISTINCT otherUser.[Id] AS [UserId]
FROM [dbo].[ChatMessages] m
JOIN [dbo].[AspNetUsers] otherUser
    ON otherUser.[Id] = CASE WHEN m.[SenderId] = @UserId THEN m.[ReceiverId] ELSE m.[SenderId] END
JOIN [dbo].[AspNetUserRoles] ur ON ur.[UserId] = otherUser.[Id]
JOIN [dbo].[AspNetRoles] r ON r.[Id] = ur.[RoleId] AND r.[NormalizedName] IN (N'ADMIN', N'EMPLOYEE')
WHERE m.[RoomType] = N'Private'
  AND m.[IsDeleted] = 0
  AND (m.[SenderId] = @UserId OR m.[ReceiverId] = @UserId)
  AND otherUser.[Id] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM [dbo].[ChatPrivateArchives] a
      WHERE a.[ConversationKey] = m.[ConversationKey]
        AND a.[ArchivedById] = otherUser.[Id]
        AND a.[IsArchived] = 1
  )
ORDER BY otherUser.[Id];";

        return await QueryAsync(sql, cmd => AddParameter(cmd, "@UserId", userId), r => GetString(r, "UserId"));
    }

    public async Task<bool> DeletePrivateConversationAsync(string conversationKey)
    {
        const string sql = @"
DELETE FROM [dbo].[ChatMessages] WHERE [ConversationKey] = @ConversationKey AND [RoomType] = N'Private';
UPDATE [dbo].[ChatPrivateArchives] SET [IsArchived] = 0, [ArchivedAt] = NULL WHERE [ConversationKey] = @ConversationKey;";
        return await ExecuteNonQueryAsync(sql, cmd => AddParameter(cmd, "@ConversationKey", conversationKey)) > 0;
    }

    public async Task<ChatSupportConversationRecord?> GetLatestVisibleSupportConversationAsync(string customerId)
    {
        const string sql = @"
SELECT TOP 1 c.[Id], c.[CustomerId], COALESCE(u.[Email], u.[UserName], N'Khách hàng') AS [CustomerName],
       c.[ConversationKey], c.[Status], c.[CreatedAt], c.[LastMessageAt], c.[ClosedAt], c.[ClosedById], c.[ArchivedAt], c.[RemovedAt],
       COALESCE(lastMsg.[Preview], N'') AS [LastMessage]
FROM [dbo].[ChatSupportConversations] c
LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = c.[CustomerId]
OUTER APPLY (
    SELECT TOP 1
        CASE
            WHEN m.[IsDeleted] = 1 THEN N'Tin nhắn đã xóa'
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[Message], N''))), N'') IS NOT NULL THEN m.[Message]
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[ImageUrl], N''))), N'') IS NOT NULL THEN N'Đã gửi hình ảnh'
            ELSE N''
        END AS [Preview]
    FROM [dbo].[ChatMessages] m
    WHERE m.[ConversationKey] = c.[ConversationKey]
    ORDER BY m.[SentAt] DESC, m.[Id] DESC
) lastMsg
WHERE c.[CustomerId] = @CustomerId AND c.[Status] IN (N'Open', N'Closed')
ORDER BY c.[LastMessageAt] DESC, c.[Id] DESC;";

        return (await QueryAsync(sql, cmd => AddParameter(cmd, "@CustomerId", customerId), MapSupportConversation)).FirstOrDefault();
    }

    public async Task<ChatSupportConversationRecord> CreateOpenSupportConversationAsync(string customerId)
    {
        var key = ChatConversationHelper.NewSupportConversationKey(customerId);
        const string sql = @"
INSERT INTO [dbo].[ChatSupportConversations] ([CustomerId], [ConversationKey], [Status], [CreatedAt], [LastMessageAt])
OUTPUT INSERTED.[Id]
VALUES (@CustomerId, @ConversationKey, N'Open', SYSUTCDATETIME(), SYSUTCDATETIME());";
        var id = await ExecuteScalarAsync<int>(sql, cmd =>
        {
            AddParameter(cmd, "@CustomerId", customerId);
            AddParameter(cmd, "@ConversationKey", key);
        });

        return (await GetSupportConversationByIdAsync(id))!;
    }

    public async Task<ChatSupportConversationRecord> GetOrCreateOpenSupportConversationAsync(string customerId)
    {
        var latest = await GetLatestVisibleSupportConversationAsync(customerId);
        if (latest != null && latest.Status == ChatSupportStatus.Open) return latest;
        if (latest != null && latest.Status == ChatSupportStatus.Closed) return latest;
        return await CreateOpenSupportConversationAsync(customerId);
    }

    public async Task<ChatSupportConversationRecord?> GetSupportConversationByIdAsync(int id)
    {
        const string sql = @"
SELECT c.[Id], c.[CustomerId], COALESCE(u.[Email], u.[UserName], N'Khách hàng') AS [CustomerName],
       c.[ConversationKey], c.[Status], c.[CreatedAt], c.[LastMessageAt], c.[ClosedAt], c.[ClosedById], c.[ArchivedAt], c.[RemovedAt],
       COALESCE(lastMsg.[Preview], N'') AS [LastMessage]
FROM [dbo].[ChatSupportConversations] c
LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = c.[CustomerId]
OUTER APPLY (
    SELECT TOP 1
        CASE
            WHEN m.[IsDeleted] = 1 THEN N'Tin nhắn đã xóa'
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[Message], N''))), N'') IS NOT NULL THEN m.[Message]
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[ImageUrl], N''))), N'') IS NOT NULL THEN N'Đã gửi hình ảnh'
            ELSE N''
        END AS [Preview]
    FROM [dbo].[ChatMessages] m
    WHERE m.[ConversationKey] = c.[ConversationKey]
    ORDER BY m.[SentAt] DESC, m.[Id] DESC
) lastMsg
WHERE c.[Id] = @Id;";
        return (await QueryAsync(sql, cmd => AddParameter(cmd, "@Id", id), MapSupportConversation)).FirstOrDefault();
    }

    public async Task<ChatSupportConversationRecord?> GetSupportConversationByKeyAsync(string conversationKey)
    {
        const string sql = @"
SELECT c.[Id], c.[CustomerId], COALESCE(u.[Email], u.[UserName], N'Khách hàng') AS [CustomerName],
       c.[ConversationKey], c.[Status], c.[CreatedAt], c.[LastMessageAt], c.[ClosedAt], c.[ClosedById], c.[ArchivedAt], c.[RemovedAt],
       COALESCE(lastMsg.[Preview], N'') AS [LastMessage]
FROM [dbo].[ChatSupportConversations] c
LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = c.[CustomerId]
OUTER APPLY (
    SELECT TOP 1
        CASE
            WHEN m.[IsDeleted] = 1 THEN N'Tin nhắn đã xóa'
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[Message], N''))), N'') IS NOT NULL THEN m.[Message]
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[ImageUrl], N''))), N'') IS NOT NULL THEN N'Đã gửi hình ảnh'
            ELSE N''
        END AS [Preview]
    FROM [dbo].[ChatMessages] m
    WHERE m.[ConversationKey] = c.[ConversationKey]
    ORDER BY m.[SentAt] DESC, m.[Id] DESC
) lastMsg
WHERE c.[ConversationKey] = @ConversationKey;";
        return (await QueryAsync(sql, cmd => AddParameter(cmd, "@ConversationKey", conversationKey), MapSupportConversation)).FirstOrDefault();
    }

    public async Task<List<ChatSupportConversationRecord>> GetSupportConversationsAsync(string status = ChatSupportStatus.Open)
    {
        const string sql = @"
SELECT c.[Id], c.[CustomerId], COALESCE(u.[Email], u.[UserName], N'Khách hàng') AS [CustomerName],
       c.[ConversationKey], c.[Status], c.[CreatedAt], c.[LastMessageAt], c.[ClosedAt], c.[ClosedById], c.[ArchivedAt], c.[RemovedAt],
       COALESCE(lastMsg.[Preview], N'') AS [LastMessage]
FROM [dbo].[ChatSupportConversations] c
LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = c.[CustomerId]
OUTER APPLY (
    SELECT TOP 1
        CASE
            WHEN m.[IsDeleted] = 1 THEN N'Tin nhắn đã xóa'
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[Message], N''))), N'') IS NOT NULL THEN m.[Message]
            WHEN NULLIF(LTRIM(RTRIM(COALESCE(m.[ImageUrl], N''))), N'') IS NOT NULL THEN N'Đã gửi hình ảnh'
            ELSE N''
        END AS [Preview]
    FROM [dbo].[ChatMessages] m
    WHERE m.[ConversationKey] = c.[ConversationKey]
    ORDER BY m.[SentAt] DESC, m.[Id] DESC
) lastMsg
WHERE c.[Status] = @Status
ORDER BY c.[LastMessageAt] DESC, c.[Id] DESC;";

        return await QueryAsync(sql, cmd => AddParameter(cmd, "@Status", status), MapSupportConversation);
    }

    public async Task<bool> UpdateSupportConversationStatusAsync(int conversationId, string status, string adminId)
    {
        if (status != ChatSupportStatus.Open && status != ChatSupportStatus.Closed && status != ChatSupportStatus.Archived) return false;

        var sql = status switch
        {
            ChatSupportStatus.Closed => @"
UPDATE [dbo].[ChatSupportConversations]
SET [Status] = N'Closed', [ClosedAt] = SYSUTCDATETIME(), [ClosedById] = @AdminId
WHERE [Id] = @Id AND [Status] = N'Open';",
            ChatSupportStatus.Archived => @"
UPDATE [dbo].[ChatSupportConversations]
SET [Status] = N'Archived', [ArchivedAt] = SYSUTCDATETIME()
WHERE [Id] = @Id AND [Status] IN (N'Open', N'Closed');",
            _ => @"
UPDATE [dbo].[ChatSupportConversations]
SET [Status] = N'Open', [ClosedAt] = NULL, [ClosedById] = NULL, [ArchivedAt] = NULL, [RemovedAt] = NULL, [LastMessageAt] = SYSUTCDATETIME()
WHERE [Id] = @Id AND [Status] IN (N'Closed', N'Archived');"
        };

        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@Id", conversationId);
            AddParameter(cmd, "@AdminId", adminId);
        }) > 0;
    }

    public async Task<ChatSupportConversationRecord?> RemoveSupportConversationAsync(int conversationId)
    {
        var conversation = await GetSupportConversationByIdAsync(conversationId);
        if (conversation == null) return null;

        const string sql = @"
DELETE FROM [dbo].[ChatMessages] WHERE [ConversationKey] = @ConversationKey;
DELETE FROM [dbo].[ChatSupportConversations] WHERE [Id] = @Id;";
        await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@ConversationKey", conversation.ConversationKey);
            AddParameter(cmd, "@Id", conversation.Id);
        });
        return conversation;
    }

    public async Task TouchSupportConversationAsync(int conversationId)
    {
        const string sql = @"UPDATE [dbo].[ChatSupportConversations] SET [LastMessageAt] = SYSUTCDATETIME() WHERE [Id] = @Id;";
        await ExecuteNonQueryAsync(sql, cmd => AddParameter(cmd, "@Id", conversationId));
    }

    public async Task<bool> IsGlobalLockedAsync()
    {
        const string sql = "SELECT [Value] FROM [dbo].[ChatSettings] WHERE [Key] = N'GlobalLocked';";
        var value = await ExecuteScalarAsync<string>(sql, _ => { });
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetGlobalLockedAsync(bool locked, string adminId)
    {
        const string sql = @"
MERGE [dbo].[ChatSettings] AS target
USING (SELECT N'GlobalLocked' AS [Key]) AS src
ON target.[Key] = src.[Key]
WHEN MATCHED THEN UPDATE SET [Value] = @Value, [UpdatedAt] = SYSUTCDATETIME(), [UpdatedById] = @AdminId
WHEN NOT MATCHED THEN INSERT ([Key], [Value], [UpdatedById]) VALUES (N'GlobalLocked', @Value, @AdminId);";
        await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@Value", locked ? "true" : "false");
            AddParameter(cmd, "@AdminId", adminId);
        });
    }

    public async Task<ChatUserRestrictionRecord?> GetActiveRestrictionAsync(string userId)
    {
        const string deactivateSql = @"
UPDATE [dbo].[ChatUserRestrictions]
SET [IsActive] = 0
WHERE [UserId] = @UserId AND [IsActive] = 1 AND [ExpiresAt] IS NOT NULL AND [ExpiresAt] <= SYSUTCDATETIME();";
        await ExecuteNonQueryAsync(deactivateSql, cmd => AddParameter(cmd, "@UserId", userId));

        const string sql = @"
SELECT TOP 1 [Id], [UserId], [RestrictionType], [Reason], [StartsAt], [ExpiresAt], [CreatedById], [CreatedAt], [IsActive]
FROM [dbo].[ChatUserRestrictions]
WHERE [UserId] = @UserId AND [IsActive] = 1
ORDER BY [CreatedAt] DESC;";
        return (await QueryAsync(sql, cmd => AddParameter(cmd, "@UserId", userId), MapRestriction)).FirstOrDefault();
    }

    public async Task AddRestrictionAsync(string userId, string restrictionType, int? minutes, string? reason, string adminId)
    {
        await RemoveRestrictionsAsync(userId);
        const string sql = @"
INSERT INTO [dbo].[ChatUserRestrictions] ([UserId], [RestrictionType], [Reason], [StartsAt], [ExpiresAt], [CreatedById], [CreatedAt], [IsActive])
VALUES (@UserId, @RestrictionType, @Reason, SYSUTCDATETIME(), @ExpiresAt, @AdminId, SYSUTCDATETIME(), 1);";
        await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddParameter(cmd, "@UserId", userId);
            AddParameter(cmd, "@RestrictionType", restrictionType);
            AddParameter(cmd, "@Reason", reason);
            AddParameter(cmd, "@ExpiresAt", minutes.HasValue ? DateTime.UtcNow.AddMinutes(minutes.Value) : null);
            AddParameter(cmd, "@AdminId", adminId);
        });
    }

    public async Task RemoveRestrictionsAsync(string userId)
    {
        const string sql = "UPDATE [dbo].[ChatUserRestrictions] SET [IsActive] = 0 WHERE [UserId] = @UserId AND [IsActive] = 1;";
        await ExecuteNonQueryAsync(sql, cmd => AddParameter(cmd, "@UserId", userId));
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Action<DbCommand> configure, Func<DbDataReader, T> map)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await _context.Database.OpenConnectionAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            configure(command);

            var result = new List<T>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add(map(reader));
            return result;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<T?> ExecuteScalarAsync<T>(string sql, Action<DbCommand> configure)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await _context.Database.OpenConnectionAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            configure(command);

            var value = await command.ExecuteScalarAsync();
            if (value == null || value == DBNull.Value) return default;
            return (T)Convert.ChangeType(value, typeof(T));
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, Action<DbCommand> configure)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await _context.Database.OpenConnectionAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            configure(command);
            return await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static ChatMessageRecord MapMessage(DbDataReader reader)
    {
        return new ChatMessageRecord
        {
            Id = GetInt(reader, "Id"),
            SenderId = GetString(reader, "SenderId"),
            SenderName = GetString(reader, "SenderName"),
            ReceiverId = GetNullableString(reader, "ReceiverId"),
            RoomType = GetString(reader, "RoomType"),
            ConversationKey = GetString(reader, "ConversationKey"),
            Message = GetNullableString(reader, "Message"),
            ImageUrl = GetNullableString(reader, "ImageUrl"),
            SentAt = GetDateTime(reader, "SentAt"),
            IsDeleted = GetBool(reader, "IsDeleted"),
            DeletedById = GetNullableString(reader, "DeletedById"),
            DeletedAt = GetNullableDateTime(reader, "DeletedAt")
        };
    }

    private static ChatPrivateRequestRecord MapPrivateRequest(DbDataReader reader)
    {
        return new ChatPrivateRequestRecord
        {
            Id = GetInt(reader, "Id"),
            RequesterId = GetString(reader, "RequesterId"),
            RequesterName = GetString(reader, "RequesterName"),
            ReceiverId = GetString(reader, "ReceiverId"),
            ReceiverName = GetString(reader, "ReceiverName"),
            ConversationKey = GetString(reader, "ConversationKey"),
            Status = GetString(reader, "Status"),
            RequestedAt = GetDateTime(reader, "RequestedAt"),
            RespondedAt = GetNullableDateTime(reader, "RespondedAt")
        };
    }

    private static ChatPrivateArchiveRecord MapPrivateArchive(DbDataReader reader)
    {
        return new ChatPrivateArchiveRecord
        {
            Id = GetInt(reader, "Id"),
            ConversationKey = GetString(reader, "ConversationKey"),
            ArchivedById = GetString(reader, "ArchivedById"),
            OtherUserId = GetString(reader, "OtherUserId"),
            OtherUserName = GetString(reader, "OtherUserName"),
            IsArchived = GetBool(reader, "IsArchived"),
            ArchivedAt = GetNullableDateTime(reader, "ArchivedAt"),
            LastMessage = GetString(reader, "LastMessage"),
            LastMessageAt = GetNullableDateTime(reader, "LastMessageAt")
        };
    }

    private static ChatSupportConversationRecord MapSupportConversation(DbDataReader reader)
    {
        return new ChatSupportConversationRecord
        {
            Id = GetInt(reader, "Id"),
            CustomerId = GetString(reader, "CustomerId"),
            CustomerName = GetString(reader, "CustomerName"),
            ConversationKey = GetString(reader, "ConversationKey"),
            Status = GetString(reader, "Status"),
            CreatedAt = GetDateTime(reader, "CreatedAt"),
            LastMessageAt = GetDateTime(reader, "LastMessageAt"),
            LastMessage = GetString(reader, "LastMessage"),
            ClosedAt = GetNullableDateTime(reader, "ClosedAt"),
            ClosedById = GetNullableString(reader, "ClosedById"),
            ArchivedAt = GetNullableDateTime(reader, "ArchivedAt"),
            RemovedAt = GetNullableDateTime(reader, "RemovedAt")
        };
    }

    private static ChatUserRestrictionRecord MapRestriction(DbDataReader reader)
    {
        return new ChatUserRestrictionRecord
        {
            Id = GetInt(reader, "Id"),
            UserId = GetString(reader, "UserId"),
            RestrictionType = GetString(reader, "RestrictionType"),
            Reason = GetNullableString(reader, "Reason"),
            StartsAt = GetDateTime(reader, "StartsAt"),
            ExpiresAt = GetNullableDateTime(reader, "ExpiresAt"),
            CreatedById = GetString(reader, "CreatedById"),
            CreatedAt = GetDateTime(reader, "CreatedAt"),
            IsActive = GetBool(reader, "IsActive")
        };
    }

    private static string GetString(DbDataReader reader, string name) => Convert.ToString(reader[name]) ?? string.Empty;
    private static string? GetNullableString(DbDataReader reader, string name) => reader[name] == DBNull.Value ? null : Convert.ToString(reader[name]);
    private static int GetInt(DbDataReader reader, string name) => Convert.ToInt32(reader[name]);
    private static bool GetBool(DbDataReader reader, string name) => Convert.ToBoolean(reader[name]);
    private static DateTime GetDateTime(DbDataReader reader, string name) => Convert.ToDateTime(reader[name]).ToLocalTime();
    private static DateTime? GetNullableDateTime(DbDataReader reader, string name) => reader[name] == DBNull.Value ? null : Convert.ToDateTime(reader[name]).ToLocalTime();

    public static readonly string SchemaSql = @"
IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatMessages] PRIMARY KEY,
        [SenderId] NVARCHAR(450) NOT NULL,
        [ReceiverId] NVARCHAR(450) NULL,
        [RoomType] NVARCHAR(30) NOT NULL,
        [ConversationKey] NVARCHAR(450) NOT NULL,
        [Message] NVARCHAR(1000) NULL,
        [ImageUrl] NVARCHAR(500) NULL,
        [SentAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatMessages_SentAt] DEFAULT SYSUTCDATETIME(),
        [IsRead] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsRead] DEFAULT 0,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsDeleted] DEFAULT 0,
        [DeletedById] NVARCHAR(450) NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_ChatMessages_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChatMessages_AspNetUsers_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'ConversationKey') IS NULL ALTER TABLE [dbo].[ChatMessages] ADD [ConversationKey] NVARCHAR(450) NULL;
    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'ImageUrl') IS NULL ALTER TABLE [dbo].[ChatMessages] ADD [ImageUrl] NVARCHAR(500) NULL;
    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'IsDeleted') IS NULL ALTER TABLE [dbo].[ChatMessages] ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsDeleted_Late] DEFAULT 0;
    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'DeletedById') IS NULL ALTER TABLE [dbo].[ChatMessages] ADD [DeletedById] NVARCHAR(450) NULL;
    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'DeletedAt') IS NULL ALTER TABLE [dbo].[ChatMessages] ADD [DeletedAt] DATETIME2 NULL;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[ChatMessages]') AND [name] = N'Message' AND is_nullable = 0)
        ALTER TABLE [dbo].[ChatMessages] ALTER COLUMN [Message] NVARCHAR(1000) NULL;
END;

IF OBJECT_ID(N'[dbo].[ChatPrivateRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatPrivateRequests]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatPrivateRequests] PRIMARY KEY,
        [RequesterId] NVARCHAR(450) NOT NULL,
        [ReceiverId] NVARCHAR(450) NOT NULL,
        [ConversationKey] NVARCHAR(450) NOT NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_ChatPrivateRequests_Status] DEFAULT N'Pending',
        [RequestedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatPrivateRequests_RequestedAt] DEFAULT SYSUTCDATETIME(),
        [RespondedAt] DATETIME2 NULL,
        CONSTRAINT [FK_ChatPrivateRequests_AspNetUsers_RequesterId] FOREIGN KEY ([RequesterId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChatPrivateRequests_AspNetUsers_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'[dbo].[ChatPrivateRequests]', N'ConversationKey') IS NULL
        ALTER TABLE [dbo].[ChatPrivateRequests] ADD [ConversationKey] NVARCHAR(450) NULL;
END;

IF COL_LENGTH(N'[dbo].[ChatPrivateRequests]', N'ConversationKey') IS NOT NULL
BEGIN
    UPDATE [dbo].[ChatPrivateRequests]
    SET [ConversationKey] = CASE
        WHEN [RequesterId] < [ReceiverId] THEN CONCAT(N'private:', [RequesterId], N':', [ReceiverId])
        ELSE CONCAT(N'private:', [ReceiverId], N':', [RequesterId])
    END
    WHERE [ConversationKey] IS NULL OR LTRIM(RTRIM([ConversationKey])) = N'';
END;

IF OBJECT_ID(N'[dbo].[ChatSupportConversations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatSupportConversations]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatSupportConversations] PRIMARY KEY,
        [CustomerId] NVARCHAR(450) NOT NULL,
        [ConversationKey] NVARCHAR(450) NOT NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_ChatSupportConversations_Status] DEFAULT N'Open',
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatSupportConversations_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [LastMessageAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatSupportConversations_LastMessageAt] DEFAULT SYSUTCDATETIME(),
        [ClosedAt] DATETIME2 NULL,
        [ClosedById] NVARCHAR(450) NULL,
        [ArchivedAt] DATETIME2 NULL,
        [RemovedAt] DATETIME2 NULL,
        CONSTRAINT [FK_ChatSupportConversations_AspNetUsers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ConversationKey') IS NULL
        ALTER TABLE [dbo].[ChatSupportConversations] ADD [ConversationKey] NVARCHAR(450) NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ClosedAt') IS NULL ALTER TABLE [dbo].[ChatSupportConversations] ADD [ClosedAt] DATETIME2 NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ClosedById') IS NULL ALTER TABLE [dbo].[ChatSupportConversations] ADD [ClosedById] NVARCHAR(450) NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ArchivedAt') IS NULL ALTER TABLE [dbo].[ChatSupportConversations] ADD [ArchivedAt] DATETIME2 NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'RemovedAt') IS NULL ALTER TABLE [dbo].[ChatSupportConversations] ADD [RemovedAt] DATETIME2 NULL;
END;

IF COL_LENGTH(N'[dbo].[ChatMessages]', N'ConversationKey') IS NOT NULL
BEGIN
    UPDATE [dbo].[ChatMessages]
    SET [ConversationKey] = CASE
        WHEN [RoomType] = N'General' THEN N'global'
        WHEN [RoomType] = N'Private' AND [ReceiverId] IS NOT NULL AND [SenderId] < [ReceiverId] THEN CONCAT(N'private:', [SenderId], N':', [ReceiverId])
        WHEN [RoomType] = N'Private' AND [ReceiverId] IS NOT NULL THEN CONCAT(N'private:', [ReceiverId], N':', [SenderId])
        WHEN [RoomType] = N'Support' AND [ReceiverId] IS NOT NULL THEN CONCAT(N'support:', [ReceiverId])
        WHEN [RoomType] = N'Support' THEN CONCAT(N'support:', [SenderId])
        ELSE N'global'
    END
    WHERE [ConversationKey] IS NULL OR LTRIM(RTRIM([ConversationKey])) = N'';
END;

UPDATE [dbo].[ChatSupportConversations]
SET [ConversationKey] = CONCAT(N'support:', [CustomerId], N':', [Id])
WHERE [ConversationKey] IS NULL OR LTRIM(RTRIM([ConversationKey])) = N'';

UPDATE m
SET m.[ConversationKey] = c.[ConversationKey]
FROM [dbo].[ChatMessages] m
INNER JOIN [dbo].[ChatSupportConversations] c ON m.[ConversationKey] = CONCAT(N'support:', c.[CustomerId]);

IF COL_LENGTH(N'[dbo].[ChatMessages]', N'ConversationKey') IS NOT NULL
BEGIN
    UPDATE [dbo].[ChatMessages]
    SET [ConversationKey] = N'global'
    WHERE [ConversationKey] IS NULL OR LTRIM(RTRIM([ConversationKey])) = N'';
END;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatSupportConversations_CustomerId' AND object_id = OBJECT_ID(N'[dbo].[ChatSupportConversations]'))
    DROP INDEX [IX_ChatSupportConversations_CustomerId] ON [dbo].[ChatSupportConversations];

IF OBJECT_ID(N'[dbo].[ChatPrivateArchives]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatPrivateArchives]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatPrivateArchives] PRIMARY KEY,
        [ConversationKey] NVARCHAR(450) NOT NULL,
        [ArchivedById] NVARCHAR(450) NOT NULL,
        [OtherUserId] NVARCHAR(450) NOT NULL,
        [IsArchived] BIT NOT NULL CONSTRAINT [DF_ChatPrivateArchives_IsArchived] DEFAULT 1,
        [ArchivedAt] DATETIME2 NULL,
        CONSTRAINT [FK_ChatPrivateArchives_AspNetUsers_ArchivedById] FOREIGN KEY ([ArchivedById]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChatPrivateArchives_AspNetUsers_OtherUserId] FOREIGN KEY ([OtherUserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'[dbo].[ChatPrivateArchives]', N'OtherUserId') IS NULL ALTER TABLE [dbo].[ChatPrivateArchives] ADD [OtherUserId] NVARCHAR(450) NULL;
    IF COL_LENGTH(N'[dbo].[ChatPrivateArchives]', N'IsArchived') IS NULL ALTER TABLE [dbo].[ChatPrivateArchives] ADD [IsArchived] BIT NOT NULL CONSTRAINT [DF_ChatPrivateArchives_IsArchived_Late] DEFAULT 1;
    IF COL_LENGTH(N'[dbo].[ChatPrivateArchives]', N'ArchivedAt') IS NULL ALTER TABLE [dbo].[ChatPrivateArchives] ADD [ArchivedAt] DATETIME2 NULL;
END;

IF OBJECT_ID(N'[dbo].[ChatUserRestrictions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatUserRestrictions]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatUserRestrictions] PRIMARY KEY,
        [UserId] NVARCHAR(450) NOT NULL,
        [RestrictionType] NVARCHAR(30) NOT NULL,
        [Reason] NVARCHAR(300) NULL,
        [StartsAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatUserRestrictions_StartsAt] DEFAULT SYSUTCDATETIME(),
        [ExpiresAt] DATETIME2 NULL,
        [CreatedById] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatUserRestrictions_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [IsActive] BIT NOT NULL CONSTRAINT [DF_ChatUserRestrictions_IsActive] DEFAULT 1,
        CONSTRAINT [FK_ChatUserRestrictions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID(N'[dbo].[ChatSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatSettings]
    (
        [Key] NVARCHAR(100) NOT NULL CONSTRAINT [PK_ChatSettings] PRIMARY KEY,
        [Value] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatSettings_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedById] NVARCHAR(450) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_ConversationKey_SentAt' AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
    CREATE INDEX [IX_ChatMessages_ConversationKey_SentAt] ON [dbo].[ChatMessages] ([ConversationKey], [SentAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_RoomType_SentAt' AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
    CREATE INDEX [IX_ChatMessages_RoomType_SentAt] ON [dbo].[ChatMessages] ([RoomType], [SentAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatPrivateRequests_ConversationKey' AND object_id = OBJECT_ID(N'[dbo].[ChatPrivateRequests]'))
    CREATE INDEX [IX_ChatPrivateRequests_ConversationKey] ON [dbo].[ChatPrivateRequests] ([ConversationKey]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatSupportConversations_ConversationKey' AND object_id = OBJECT_ID(N'[dbo].[ChatSupportConversations]'))
    CREATE UNIQUE INDEX [IX_ChatSupportConversations_ConversationKey] ON [dbo].[ChatSupportConversations] ([ConversationKey]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatSupportConversations_CustomerId_Status' AND object_id = OBJECT_ID(N'[dbo].[ChatSupportConversations]'))
    CREATE INDEX [IX_ChatSupportConversations_CustomerId_Status] ON [dbo].[ChatSupportConversations] ([CustomerId], [Status], [LastMessageAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatSupportConversations_Status' AND object_id = OBJECT_ID(N'[dbo].[ChatSupportConversations]'))
    CREATE INDEX [IX_ChatSupportConversations_Status] ON [dbo].[ChatSupportConversations] ([Status], [LastMessageAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatUserRestrictions_UserId_IsActive' AND object_id = OBJECT_ID(N'[dbo].[ChatUserRestrictions]'))
    CREATE INDEX [IX_ChatUserRestrictions_UserId_IsActive] ON [dbo].[ChatUserRestrictions] ([UserId], [IsActive]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatPrivateArchives_Admin_Conversation' AND object_id = OBJECT_ID(N'[dbo].[ChatPrivateArchives]'))
    CREATE UNIQUE INDEX [IX_ChatPrivateArchives_Admin_Conversation] ON [dbo].[ChatPrivateArchives] ([ArchivedById], [ConversationKey]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatPrivateArchives_Admin_IsArchived' AND object_id = OBJECT_ID(N'[dbo].[ChatPrivateArchives]'))
    CREATE INDEX [IX_ChatPrivateArchives_Admin_IsArchived] ON [dbo].[ChatPrivateArchives] ([ArchivedById], [IsArchived], [ArchivedAt]);

IF NOT EXISTS (SELECT 1 FROM [dbo].[ChatSettings] WHERE [Key] = N'GlobalLocked')
    INSERT INTO [dbo].[ChatSettings] ([Key], [Value]) VALUES (N'GlobalLocked', N'false');
";
}
