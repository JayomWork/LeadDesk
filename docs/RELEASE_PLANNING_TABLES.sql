/*
    LeadDesk upgrade: release planning tables
    Date: 2026-07-04

    Review and execute this script manually against the LeadDesk database
    if ReleasePlans and ReleaseWorkItems do not already exist.

    This script is idempotent and does not modify existing release data.
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[ReleasePlans]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[ReleasePlans]
        (
            [Id] bigint IDENTITY(1,1) NOT NULL,
            [Uuid] uniqueidentifier NOT NULL CONSTRAINT [DF_ReleasePlans_Uuid] DEFAULT NEWID(),
            [Version] nvarchar(100) NOT NULL,
            [Status] nvarchar(50) NOT NULL CONSTRAINT [DF_ReleasePlans_Status] DEFAULT N'Planned',
            [ReleaseDate] datetime2 NULL,
            [ReleaseNotes] nvarchar(max) NULL,
            [QaSignedOff] bit NOT NULL CONSTRAINT [DF_ReleasePlans_QaSignedOff] DEFAULT 0,
            [ClientSignedOff] bit NOT NULL CONSTRAINT [DF_ReleasePlans_ClientSignedOff] DEFAULT 0,
            [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_ReleasePlans_CreatedAt] DEFAULT SYSUTCDATETIME(),

            CONSTRAINT [PK_ReleasePlans]
                PRIMARY KEY ([Id])
        );

        CREATE INDEX [IX_ReleasePlans_Status_ReleaseDate]
            ON [dbo].[ReleasePlans] ([Status], [ReleaseDate]);
    END;

    IF OBJECT_ID(N'[dbo].[ReleaseWorkItems]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[ReleaseWorkItems]
        (
            [Id] bigint IDENTITY(1,1) NOT NULL,
            [ReleasePlanId] bigint NOT NULL,
            [WorkItemId] bigint NOT NULL,

            CONSTRAINT [PK_ReleaseWorkItems]
                PRIMARY KEY ([Id]),

            CONSTRAINT [FK_ReleaseWorkItems_ReleasePlans_ReleasePlanId]
                FOREIGN KEY ([ReleasePlanId])
                REFERENCES [dbo].[ReleasePlans] ([Id])
                ON DELETE CASCADE,

            CONSTRAINT [FK_ReleaseWorkItems_WorkItems_WorkItemId]
                FOREIGN KEY ([WorkItemId])
                REFERENCES [dbo].[WorkItems] ([Id])
                ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX [IX_ReleaseWorkItems_ReleasePlanId_WorkItemId]
            ON [dbo].[ReleaseWorkItems] ([ReleasePlanId], [WorkItemId]);

        CREATE INDEX [IX_ReleaseWorkItems_WorkItemId]
            ON [dbo].[ReleaseWorkItems] ([WorkItemId]);
    END;

    COMMIT TRANSACTION;

    SELECT
        (SELECT COUNT(*) FROM [dbo].[ReleasePlans]) AS [ReleasePlans],
        (SELECT COUNT(*) FROM [dbo].[ReleaseWorkItems]) AS [ReleaseTaskLinks];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
