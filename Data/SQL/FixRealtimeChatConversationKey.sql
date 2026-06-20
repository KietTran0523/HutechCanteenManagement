/* Fix schema realtime chat: thiếu cột ConversationKey do database đang là bản chat cũ. */

IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'ConversationKey') IS NULL
        ALTER TABLE [dbo].[ChatMessages] ADD [ConversationKey] NVARCHAR(450) NULL;

    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'ImageUrl') IS NULL
        ALTER TABLE [dbo].[ChatMessages] ADD [ImageUrl] NVARCHAR(500) NULL;

    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'IsDeleted') IS NULL
        ALTER TABLE [dbo].[ChatMessages] ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsDeleted_Fix] DEFAULT 0;

    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'DeletedById') IS NULL
        ALTER TABLE [dbo].[ChatMessages] ADD [DeletedById] NVARCHAR(450) NULL;

    IF COL_LENGTH(N'[dbo].[ChatMessages]', N'DeletedAt') IS NULL
        ALTER TABLE [dbo].[ChatMessages] ADD [DeletedAt] DATETIME2 NULL;

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

IF OBJECT_ID(N'[dbo].[ChatPrivateRequests]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[dbo].[ChatPrivateRequests]', N'ConversationKey') IS NULL
        ALTER TABLE [dbo].[ChatPrivateRequests] ADD [ConversationKey] NVARCHAR(450) NULL;

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
        [ConversationKey] NVARCHAR(450) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_ChatSupportConversations_Status_Fix] DEFAULT N'Open',
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatSupportConversations_CreatedAt_Fix] DEFAULT SYSUTCDATETIME(),
        [LastMessageAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatSupportConversations_LastMessageAt_Fix] DEFAULT SYSUTCDATETIME(),
        [ClosedAt] DATETIME2 NULL,
        [ClosedById] NVARCHAR(450) NULL,
        [ArchivedAt] DATETIME2 NULL,
        [RemovedAt] DATETIME2 NULL
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ConversationKey') IS NULL
        ALTER TABLE [dbo].[ChatSupportConversations] ADD [ConversationKey] NVARCHAR(450) NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ClosedAt') IS NULL
        ALTER TABLE [dbo].[ChatSupportConversations] ADD [ClosedAt] DATETIME2 NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ClosedById') IS NULL
        ALTER TABLE [dbo].[ChatSupportConversations] ADD [ClosedById] NVARCHAR(450) NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'ArchivedAt') IS NULL
        ALTER TABLE [dbo].[ChatSupportConversations] ADD [ArchivedAt] DATETIME2 NULL;
    IF COL_LENGTH(N'[dbo].[ChatSupportConversations]', N'RemovedAt') IS NULL
        ALTER TABLE [dbo].[ChatSupportConversations] ADD [RemovedAt] DATETIME2 NULL;
END;

UPDATE [dbo].[ChatSupportConversations]
SET [ConversationKey] = CONCAT(N'support:', [CustomerId], N':', [Id])
WHERE [ConversationKey] IS NULL OR LTRIM(RTRIM([ConversationKey])) = N'';

IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NOT NULL
BEGIN
    UPDATE m
    SET m.[ConversationKey] = c.[ConversationKey]
    FROM [dbo].[ChatMessages] m
    INNER JOIN [dbo].[ChatSupportConversations] c
        ON m.[ConversationKey] = CONCAT(N'support:', c.[CustomerId]);

    UPDATE [dbo].[ChatMessages]
    SET [ConversationKey] = N'global'
    WHERE [ConversationKey] IS NULL OR LTRIM(RTRIM([ConversationKey])) = N'';
END;

IF OBJECT_ID(N'[dbo].[ChatUserRestrictions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatUserRestrictions]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatUserRestrictions] PRIMARY KEY,
        [UserId] NVARCHAR(450) NOT NULL,
        [RestrictionType] NVARCHAR(30) NOT NULL,
        [Reason] NVARCHAR(300) NULL,
        [StartsAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatUserRestrictions_StartsAt_Fix] DEFAULT SYSUTCDATETIME(),
        [ExpiresAt] DATETIME2 NULL,
        [CreatedById] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatUserRestrictions_CreatedAt_Fix] DEFAULT SYSUTCDATETIME(),
        [IsActive] BIT NOT NULL CONSTRAINT [DF_ChatUserRestrictions_IsActive_Fix] DEFAULT 1
    );
END;

IF OBJECT_ID(N'[dbo].[ChatSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatSettings]
    (
        [Key] NVARCHAR(100) NOT NULL CONSTRAINT [PK_ChatSettings] PRIMARY KEY,
        [Value] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatSettings_UpdatedAt_Fix] DEFAULT SYSUTCDATETIME(),
        [UpdatedById] NVARCHAR(450) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[ChatSettings] WHERE [Key] = N'GlobalLocked')
    INSERT INTO [dbo].[ChatSettings] ([Key], [Value]) VALUES (N'GlobalLocked', N'false');

IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_ConversationKey_SentAt' AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
        CREATE INDEX [IX_ChatMessages_ConversationKey_SentAt] ON [dbo].[ChatMessages] ([ConversationKey], [SentAt]);
END;

IF OBJECT_ID(N'[dbo].[ChatSupportConversations]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatSupportConversations_ConversationKey' AND object_id = OBJECT_ID(N'[dbo].[ChatSupportConversations]'))
        CREATE INDEX [IX_ChatSupportConversations_ConversationKey] ON [dbo].[ChatSupportConversations] ([ConversationKey]);
END;
