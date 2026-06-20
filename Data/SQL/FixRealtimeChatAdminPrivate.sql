-- Chạy trong đúng database của project, ví dụ: QuanLyCanTeenHutech
-- Script này bổ sung bảng archive cho chat riêng admin và an toàn khi chạy nhiều lần.

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
GO

IF OBJECT_ID(N'[dbo].[ChatPrivateArchives]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.ChatPrivateArchives', 'OtherUserId') IS NULL
        ALTER TABLE [dbo].[ChatPrivateArchives] ADD [OtherUserId] NVARCHAR(450) NULL;
END
GO

IF OBJECT_ID(N'[dbo].[ChatPrivateArchives]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.ChatPrivateArchives', 'IsArchived') IS NULL
        ALTER TABLE [dbo].[ChatPrivateArchives] ADD [IsArchived] BIT NOT NULL CONSTRAINT [DF_ChatPrivateArchives_IsArchived_Late] DEFAULT 1;
END
GO

IF OBJECT_ID(N'[dbo].[ChatPrivateArchives]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.ChatPrivateArchives', 'ArchivedAt') IS NULL
        ALTER TABLE [dbo].[ChatPrivateArchives] ADD [ArchivedAt] DATETIME2 NULL;
END
GO

IF OBJECT_ID(N'[dbo].[ChatPrivateArchives]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_ChatPrivateArchives_Admin_Conversation'
          AND object_id = OBJECT_ID(N'[dbo].[ChatPrivateArchives]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_ChatPrivateArchives_Admin_Conversation]
        ON [dbo].[ChatPrivateArchives] ([ArchivedById], [ConversationKey]);
    END
END
GO

IF OBJECT_ID(N'[dbo].[ChatPrivateArchives]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_ChatPrivateArchives_Admin_IsArchived'
          AND object_id = OBJECT_ID(N'[dbo].[ChatPrivateArchives]')
    )
    BEGIN
        CREATE INDEX [IX_ChatPrivateArchives_Admin_IsArchived]
        ON [dbo].[ChatPrivateArchives] ([ArchivedById], [IsArchived], [ArchivedAt]);
    END
END
GO
