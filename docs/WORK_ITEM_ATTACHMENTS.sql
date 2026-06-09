CREATE TABLE [dbo].[WorkItemAttachments]
(
    [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorkItemAttachments] PRIMARY KEY,
    [Uuid] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_WorkItemAttachments_Uuid] DEFAULT NEWID(),
    [WorkItemId] BIGINT NOT NULL,
    [FileName] NVARCHAR(260) NOT NULL,
    [StoragePath] NVARCHAR(500) NOT NULL,
    [StorageProvider] NVARCHAR(50) NOT NULL CONSTRAINT [DF_WorkItemAttachments_StorageProvider] DEFAULT N'Local',
    [ContentType] NVARCHAR(150) NULL,
    [FileSizeBytes] BIGINT NOT NULL CONSTRAINT [DF_WorkItemAttachments_FileSizeBytes] DEFAULT 0,
    [SourceType] NVARCHAR(30) NOT NULL CONSTRAINT [DF_WorkItemAttachments_SourceType] DEFAULT N'Document',
    [EmailSubject] NVARCHAR(250) NULL,
    [EmailFrom] NVARCHAR(200) NULL,
    [EmailReceivedAt] DATETIME2 NULL,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_WorkItemAttachments_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [FK_WorkItemAttachments_WorkItems_WorkItemId]
        FOREIGN KEY ([WorkItemId]) REFERENCES [dbo].[WorkItems]([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_WorkItemAttachments_WorkItemId_CreatedAt]
ON [dbo].[WorkItemAttachments] ([WorkItemId], [CreatedAt]);
