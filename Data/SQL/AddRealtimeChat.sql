IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChatMessages] PRIMARY KEY,
        [SenderId] NVARCHAR(450) NOT NULL,
        [ReceiverId] NVARCHAR(450) NULL,
        [RoomType] NVARCHAR(30) NOT NULL,
        [ConversationKey] NVARCHAR(450) NOT NULL,
        [Message] NVARCHAR(1000) NOT NULL,
        [SentAt] DATETIME2 NOT NULL,
        [IsRead] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsRead] DEFAULT(0),
        CONSTRAINT [FK_ChatMessages_AspNetUsers_SenderId]
            FOREIGN KEY ([SenderId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChatMessages_AspNetUsers_ReceiverId]
            FOREIGN KEY ([ReceiverId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_ConversationKey_SentAt' AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
BEGIN
    CREATE INDEX [IX_ChatMessages_ConversationKey_SentAt]
    ON [dbo].[ChatMessages] ([ConversationKey], [SentAt]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_SenderId' AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
BEGIN
    CREATE INDEX [IX_ChatMessages_SenderId]
    ON [dbo].[ChatMessages] ([SenderId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_ReceiverId' AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
BEGIN
    CREATE INDEX [IX_ChatMessages_ReceiverId]
    ON [dbo].[ChatMessages] ([ReceiverId]);
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260620000300_AddRealtimeChat')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260620000300_AddRealtimeChat', N'10.0.9');
END;
