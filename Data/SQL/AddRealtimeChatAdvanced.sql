
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

UPDATE [dbo].[ChatSupportConversations]
SET [ConversationKey] = CONCAT(N'support:', [CustomerId], N':', [Id])
WHERE [ConversationKey] IS NULL OR LTRIM(RTRIM([ConversationKey])) = N'';

UPDATE m
SET m.[ConversationKey] = c.[ConversationKey]
FROM [dbo].[ChatMessages] m
INNER JOIN [dbo].[ChatSupportConversations] c ON m.[ConversationKey] = CONCAT(N'support:', c.[CustomerId]);

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
