/*
    LeadDesk upgrade: multiple task accounts and developers
    Date: 2026-06-22

    Review and execute this script manually against the LeadDesk database
    before running the upgraded application.

    The script is idempotent: it can be run again safely. Existing single
    account/developer assignments are copied into the new link tables.
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[WorkItemDevelopers]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[WorkItemDevelopers]
        (
            [WorkItemId]  bigint NOT NULL,
            [TeamMemberId] bigint NOT NULL,

            CONSTRAINT [PK_WorkItemDevelopers]
                PRIMARY KEY ([WorkItemId], [TeamMemberId]),

            CONSTRAINT [FK_WorkItemDevelopers_WorkItems_WorkItemId]
                FOREIGN KEY ([WorkItemId])
                REFERENCES [dbo].[WorkItems] ([Id])
                ON DELETE CASCADE,

            CONSTRAINT [FK_WorkItemDevelopers_TeamMembers_TeamMemberId]
                FOREIGN KEY ([TeamMemberId])
                REFERENCES [dbo].[TeamMembers] ([Id])
        );

        CREATE INDEX [IX_WorkItemDevelopers_TeamMemberId]
            ON [dbo].[WorkItemDevelopers] ([TeamMemberId]);
    END;

    IF OBJECT_ID(N'[dbo].[WorkItemAccounts]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[WorkItemAccounts]
        (
            [WorkItemId]       bigint NOT NULL,
            [AccountPracticeId] bigint NOT NULL,

            CONSTRAINT [PK_WorkItemAccounts]
                PRIMARY KEY ([WorkItemId], [AccountPracticeId]),

            CONSTRAINT [FK_WorkItemAccounts_WorkItems_WorkItemId]
                FOREIGN KEY ([WorkItemId])
                REFERENCES [dbo].[WorkItems] ([Id])
                ON DELETE CASCADE,

            CONSTRAINT [FK_WorkItemAccounts_AccountPractices_AccountPracticeId]
                FOREIGN KEY ([AccountPracticeId])
                REFERENCES [dbo].[AccountPractices] ([Id])
        );

        CREATE INDEX [IX_WorkItemAccounts_AccountPracticeId]
            ON [dbo].[WorkItemAccounts] ([AccountPracticeId]);
    END;

    /* Preserve current single-developer assignments. */
    INSERT INTO [dbo].[WorkItemDevelopers] ([WorkItemId], [TeamMemberId])
    SELECT w.[Id], w.[AssignedDeveloperId]
    FROM [dbo].[WorkItems] AS w
    WHERE w.[AssignedDeveloperId] IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[WorkItemDevelopers] AS d
          WHERE d.[WorkItemId] = w.[Id]
            AND d.[TeamMemberId] = w.[AssignedDeveloperId]
      );

    /* Preserve current single-account assignments. */
    INSERT INTO [dbo].[WorkItemAccounts] ([WorkItemId], [AccountPracticeId])
    SELECT w.[Id], w.[AccountPracticeId]
    FROM [dbo].[WorkItems] AS w
    WHERE w.[AccountPracticeId] IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[WorkItemAccounts] AS a
          WHERE a.[WorkItemId] = w.[Id]
            AND a.[AccountPracticeId] = w.[AccountPracticeId]
      );

    COMMIT TRANSACTION;

    SELECT
        (SELECT COUNT(*) FROM [dbo].[WorkItemDevelopers]) AS [TaskDeveloperLinks],
        (SELECT COUNT(*) FROM [dbo].[WorkItemAccounts]) AS [TaskAccountLinks];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
