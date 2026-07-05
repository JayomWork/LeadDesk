using Microsoft.EntityFrameworkCore;
using ProjectPulse.Application;
using ProjectPulse.Domain;

namespace ProjectPulse.Infrastructure;

public sealed class LeadDeskService(IDbContextFactory<LeadDeskDbContext> contextFactory) : ILeadDeskService
{
    private static readonly string[] ArchivedStatuses = ["Released", "Completed", "Cancelled"];
    private static readonly string[] ReleaseCandidateStatuses = ["QA Done", "Ready for UAT", "Ready for Release"];
    private static readonly string[] QaStatuses = ["Ready for QA", "In QA", "QA Failed"];
    private static readonly string[] ActiveReleaseStatuses = ["Planned", "In Progress", "Ready"];
    private static readonly string[] PendingStatuses = ["New", "Analysis", "Ready for Dev"];
    private static readonly string[] InQueueStatuses = ["In Dev", "Dev Done", "Ready for QA", "In QA", "QA Failed", "Ready for UAT", "Ready for Release"];

    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var weekEnd = now.AddDays(7);
        return new DashboardSummary
        {
            TotalOpen = await db.WorkItems.CountAsync(x => !ArchivedStatuses.Contains(x.Status), cancellationToken),
            InDevelopment = await db.WorkItems.CountAsync(x => x.Status == "In Dev", cancellationToken),
            ReadyForQa = await db.WorkItems.CountAsync(x => x.Status == "Ready for QA", cancellationToken),
            Blocked = await db.WorkItems.CountAsync(x => x.Status == "Blocked", cancellationToken),
            DueThisWeek = await db.WorkItems.CountAsync(x => x.DueDate != null && x.DueDate <= weekEnd && !ArchivedStatuses.Contains(x.Status), cancellationToken),
            ReleasedThisMonth = await db.WorkItems.CountAsync(x => x.Status == "Released" && x.UpdatedAt != null && x.UpdatedAt.Value.Month == now.Month && x.UpdatedAt.Value.Year == now.Year, cancellationToken),
            RecentItems = await TaskGraph(db.WorkItems).OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).Take(8).ToListAsync(cancellationToken)
        };
    }

    public async Task<List<WorkItem>> GetTasksAsync(TaskQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var items = TaskGraph(db.WorkItems).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search)) items = items.Where(x => x.Title.Contains(query.Search) || (x.Description != null && x.Description.Contains(query.Search)) || (x.LatestUpdate != null && x.LatestUpdate.Contains(query.Search)));
        if (!string.IsNullOrWhiteSpace(query.Status)) items = items.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Priority)) items = items.Where(x => x.Priority == query.Priority);
        if (query.AccountId.HasValue) items = items.Where(x => x.AccountPracticeId == query.AccountId || x.WorkItemAccounts.Any(a => a.AccountPracticeId == query.AccountId));
        if (query.DeveloperId.HasValue) items = items.Where(x => x.AssignedDeveloperId == query.DeveloperId || x.WorkItemDevelopers.Any(d => d.TeamMemberId == query.DeveloperId));
        if (!query.IncludeArchived && string.IsNullOrWhiteSpace(query.Status)) items = items.Where(x => !ArchivedStatuses.Contains(x.Status));
        items = query.SortBy switch
        {
            "Newest" => items.OrderByDescending(x => x.CreatedAt),
            "Oldest" => items.OrderBy(x => x.CreatedAt),
            "Title" => items.OrderBy(x => x.Title),
            "Priority" => items.OrderBy(x => x.Priority == "Critical" ? 0 : x.Priority == "High" ? 1 : x.Priority == "Medium" ? 2 : 3).ThenByDescending(x => x.CreatedAt),
            _ => items.OrderBy(x =>
                    ActiveReleaseStatuses.Contains(x.Status) ? 0 :
                    ReleaseCandidateStatuses.Contains(x.Status) ? 0 :
                    QaStatuses.Contains(x.Status) ? 1 :
                    x.Status == "Dev Done" ? 2 :
                    x.Status == "In Dev" || x.Status == "Ready for Dev" ? 3 :
                    x.Status == "New" || x.Status == "Analysis" ? 4 : 5)
                .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
        };
        return await items.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<WorkItem?> GetTaskAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await TaskGraph(db.WorkItems).Include(x => x.Owner).Include(x => x.Updates).ThenInclude(x => x.UpdatedBy).Include(x => x.Attachments).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item != null)
        {
            item.SelectedAccountIds = item.WorkItemAccounts.Select(x => x.AccountPracticeId).ToList();
            item.SelectedDeveloperIds = item.WorkItemDevelopers.Select(x => x.TeamMemberId).ToList();
            if (item.SelectedAccountIds.Count == 0 && item.AccountPracticeId.HasValue) item.SelectedAccountIds.Add(item.AccountPracticeId.Value);
            if (item.SelectedDeveloperIds.Count == 0 && item.AssignedDeveloperId.HasValue) item.SelectedDeveloperIds.Add(item.AssignedDeveloperId.Value);
        }
        return item;
    }

    public async Task<long> SaveTaskAsync(WorkItem input, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = input.Id == 0 ? new WorkItem() : await db.WorkItems.FirstOrDefaultAsync(x => x.Id == input.Id, cancellationToken) ?? throw new InvalidOperationException("Task not found.");
        var oldStatus = item.Status;
        CopyTask(input, item);
        item.Title = item.Title.Trim();
        if (string.IsNullOrWhiteSpace(item.Title)) throw new InvalidOperationException("Task title is required.");
        item.AssignedDeveloperId = input.SelectedDeveloperIds.FirstOrDefault() is var dev && dev > 0 ? dev : null;
        item.AccountPracticeId = input.SelectedAccountIds.FirstOrDefault() is var account && account > 0 ? account : null;
        item.UpdatedAt = input.Id == 0 ? null : DateTime.UtcNow;
        if (input.Id == 0) db.WorkItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        await db.WorkItemDevelopers.Where(x => x.WorkItemId == item.Id).ExecuteDeleteAsync(cancellationToken);
        await db.WorkItemAccounts.Where(x => x.WorkItemId == item.Id).ExecuteDeleteAsync(cancellationToken);
        db.WorkItemDevelopers.AddRange(input.SelectedDeveloperIds.Distinct().Select(id => new WorkItemDeveloper { WorkItemId = item.Id, TeamMemberId = id }));
        db.WorkItemAccounts.AddRange(input.SelectedAccountIds.Distinct().Select(id => new WorkItemAccount { WorkItemId = item.Id, AccountPracticeId = id }));
        if (input.Id != 0 && oldStatus != item.Status) db.WorkItemUpdates.Add(new WorkItemUpdate { WorkItemId = item.Id, OldStatus = oldStatus, NewStatus = item.Status, UpdateText = $"Status changed from {oldStatus} to {item.Status}." });
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public async Task UpdateTaskStatusAsync(long id, string status, CancellationToken cancellationToken = default)
    {
        if (!LeadDeskOptions.Statuses.Contains(status)) throw new ArgumentOutOfRangeException(nameof(status));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.WorkItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("Task not found.");
        if (item.Status == status) return;
        var old = item.Status;
        item.Status = status;
        item.LatestUpdate = $"Status changed from {old} to {status}.";
        item.UpdatedAt = DateTime.UtcNow;
        db.WorkItemUpdates.Add(new WorkItemUpdate { WorkItemId = id, OldStatus = old, NewStatus = status, UpdateText = item.LatestUpdate });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveTaskAsync(long id, CancellationToken cancellationToken = default) => await UpdateTaskStatusAsync(id, "Completed", cancellationToken);

    public async Task<List<AccountPractice>> GetAccountsAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.AccountPractices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search));
        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<AccountTaskSummary>> GetAccountTaskSummariesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var accountQuery = db.AccountPractices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) accountQuery = accountQuery.Where(x => x.Name.Contains(search));

        var accounts = await accountQuery.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToHashSet();
        if (accountIds.Count == 0) return [];

        var tasks = await db.WorkItems
            .Include(x => x.WorkItemAccounts)
            .Include(x => x.Owner)
            .Include(x => x.AssignedDeveloper)
            .AsNoTracking()
            .Where(x =>
                (x.AccountPracticeId.HasValue && accountIds.Contains(x.AccountPracticeId.Value)) ||
                x.WorkItemAccounts.Any(a => accountIds.Contains(a.AccountPracticeId)))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var meetings = await db.Meetings
            .AsNoTracking()
            .Where(x => x.AccountPracticeId.HasValue && accountIds.Contains(x.AccountPracticeId.Value))
            .ToListAsync(cancellationToken);

        return accounts.Select(account =>
        {
            var accountTasks = tasks
                .Where(task =>
                    task.AccountPracticeId == account.Id ||
                    task.WorkItemAccounts.Any(link => link.AccountPracticeId == account.Id))
                .DistinctBy(task => task.Id)
                .ToList();
            var activeTasks = accountTasks.Where(task => !ArchivedStatuses.Contains(task.Status)).ToList();
            var lastTaskUpdate = accountTasks
                .Select(task => task.UpdatedAt ?? task.CreatedAt)
                .DefaultIfEmpty(account.UpdatedAt ?? account.CreatedAt)
                .Max();
            var ownerName = activeTasks
                .Select(task => task.Owner?.FullName ?? task.AssignedDeveloper?.FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name!)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .FirstOrDefault() ?? "Unassigned";

            return new AccountTaskSummary
            {
                Account = account,
                TotalTasks = accountTasks.Count,
                PendingTasks = accountTasks.Count(task => PendingStatuses.Contains(task.Status)),
                InQueueTasks = accountTasks.Count(task => InQueueStatuses.Contains(task.Status)),
                InProgressTasks = accountTasks.Count(task => task.Status is "In Dev" or "Dev Done"),
                QaTasks = accountTasks.Count(task => task.Status is "Ready for QA" or "In QA" or "QA Failed"),
                ReleaseReadyTasks = accountTasks.Count(task => ReleaseCandidateStatuses.Contains(task.Status)),
                OverdueTasks = accountTasks.Count(task => task.DueDate.HasValue && task.DueDate.Value.Date < now.Date && !ArchivedStatuses.Contains(task.Status)),
                BlockedTasks = accountTasks.Count(task => task.Status == "Blocked"),
                DoneTasks = accountTasks.Count(task => ArchivedStatuses.Contains(task.Status)),
                OwnerName = ownerName,
                Priority = AccountPriority(activeTasks),
                LastUpdatedAt = new[] { account.UpdatedAt ?? account.CreatedAt, lastTaskUpdate }.Max(),
                NextMeetingDate = meetings
                    .Where(meeting => meeting.AccountPracticeId == account.Id && meeting.MeetingDate >= now)
                    .OrderBy(meeting => meeting.MeetingDate)
                    .Select(meeting => (DateTime?)meeting.MeetingDate)
                    .FirstOrDefault(),
                TaskStatuses = accountTasks.Select(task => task.Status).Distinct().OrderBy(status => status).ToList()
            };
        }).ToList();
    }

    public async Task<AccountPractice?> GetAccountAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AccountPractices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<long> SaveAccountAsync(AccountPractice input, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = input.Id == 0 ? new AccountPractice() : await db.AccountPractices.FindAsync([input.Id], cancellationToken) ?? throw new InvalidOperationException("Account not found.");
        item.Name = input.Name; item.ClientContactName = input.ClientContactName; item.ClientContactEmail = input.ClientContactEmail; item.Status = input.Status; item.GoLiveDate = input.GoLiveDate; item.Notes = input.Notes; item.IsActive = input.IsActive; item.UpdatedAt = input.Id == 0 ? null : DateTime.UtcNow;
        if (input.Id == 0) db.AccountPractices.Add(item);
        await db.SaveChangesAsync(cancellationToken); return item.Id;
    }

    public async Task<List<TeamMember>> GetTeamAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.TeamMembers.AsNoTracking(); if (activeOnly) query = query.Where(x => x.IsActive);
        return await query.OrderBy(x => x.FullName).ToListAsync(cancellationToken);
    }

    public async Task<List<TeamMemberTaskSummary>> GetTeamTaskSummariesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var memberQuery = db.TeamMembers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            memberQuery = memberQuery.Where(x => x.FullName.Contains(search) || (x.Email != null && x.Email.Contains(search)));
        }

        var members = await memberQuery.OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        var memberIds = members.Select(x => x.Id).ToHashSet();
        if (memberIds.Count == 0) return [];

        var tasks = await TaskGraph(db.WorkItems)
            .Include(x => x.Owner)
            .AsNoTracking()
            .Where(x =>
                (x.AssignedDeveloperId.HasValue && memberIds.Contains(x.AssignedDeveloperId.Value)) ||
                (x.AssignedQaId.HasValue && memberIds.Contains(x.AssignedQaId.Value)) ||
                (x.OwnerId.HasValue && memberIds.Contains(x.OwnerId.Value)) ||
                x.WorkItemDevelopers.Any(d => memberIds.Contains(d.TeamMemberId)))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        return members.Select(member =>
        {
            var memberTasks = tasks
                .Where(task =>
                    task.AssignedDeveloperId == member.Id ||
                    task.AssignedQaId == member.Id ||
                    task.OwnerId == member.Id ||
                    task.WorkItemDevelopers.Any(link => link.TeamMemberId == member.Id))
                .DistinctBy(task => task.Id)
                .ToList();

            var pending = memberTasks.Count(task => PendingStatuses.Contains(task.Status));
            var completed = memberTasks.Count(task => ArchivedStatuses.Contains(task.Status));
            var active = memberTasks.Where(task => !ArchivedStatuses.Contains(task.Status)).ToList();
            var inProgress = active.Count - pending;
            if (inProgress < 0) inProgress = 0;

            var accountNames = memberTasks
                .SelectMany(TaskAccountNames)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            var primaryAccount = accountNames
                .GroupBy(name => name)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .FirstOrDefault() ?? "Unassigned";

            return new TeamMemberTaskSummary
            {
                Member = member,
                TotalTasks = memberTasks.Count,
                PendingTaskCount = pending,
                InProgressTaskCount = inProgress,
                CompletedTaskCount = completed,
                OverdueTaskCount = memberTasks.Count(task => task.DueDate.HasValue && task.DueDate.Value.Date < now.Date && !ArchivedStatuses.Contains(task.Status)),
                CompletionPercentage = memberTasks.Count == 0 ? 0 : (int)Math.Round(completed * 100d / memberTasks.Count),
                PrimaryAccountName = primaryAccount,
                AssignedAccounts = accountNames.Distinct().OrderBy(name => name).ToList(),
                LastUpdatedAt = memberTasks.Select(task => task.UpdatedAt ?? task.CreatedAt).DefaultIfEmpty(member.CreatedAt).Max(),
                RecentTasks = memberTasks
                    .OrderByDescending(task => task.UpdatedAt ?? task.CreatedAt)
                    .Select(task => new TeamMemberRecentTask
                    {
                        Id = task.Id,
                        Title = task.Title,
                        AccountName = TaskAccountNames(task).FirstOrDefault() ?? "All accounts",
                        Status = task.Status,
                        Priority = task.Priority,
                        DueDate = task.DueDate
                    })
                    .ToList()
            };
        }).ToList();
    }

    public async Task<TeamMember?> GetTeamMemberAsync(long id, CancellationToken cancellationToken = default)
    { await using var db = await contextFactory.CreateDbContextAsync(cancellationToken); return await db.TeamMembers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken); }

    public async Task<long> SaveTeamMemberAsync(TeamMember input, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = input.Id == 0 ? new TeamMember() : await db.TeamMembers.FindAsync([input.Id], cancellationToken) ?? throw new InvalidOperationException("Team member not found.");
        item.FullName = input.FullName; item.Role = input.Role; item.Email = input.Email; item.IsActive = input.IsActive;
        if (input.Id == 0) db.TeamMembers.Add(item); await db.SaveChangesAsync(cancellationToken); return item.Id;
    }

    public async Task<List<Meeting>> GetMeetingsAsync(CancellationToken cancellationToken = default)
    { await using var db = await contextFactory.CreateDbContextAsync(cancellationToken); return await db.Meetings.Include(x => x.AccountPractice).Include(x => x.MeetingWorkItems).ThenInclude(x => x.WorkItem).AsNoTracking().OrderByDescending(x => x.MeetingDate).ToListAsync(cancellationToken); }

    public async Task<Meeting?> GetMeetingAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Meetings.Include(x => x.AccountPractice).Include(x => x.MeetingWorkItems).ThenInclude(x => x.WorkItem).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item != null) item.SelectedWorkItemIds = item.MeetingWorkItems.Select(x => x.WorkItemId).ToList(); return item;
    }

    public async Task<long> SaveMeetingAsync(Meeting input, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = input.Id == 0 ? new Meeting() : await db.Meetings.FindAsync([input.Id], cancellationToken) ?? throw new InvalidOperationException("Meeting not found.");
        item.Title = input.Title; item.MeetingType = input.MeetingType; item.AccountPracticeId = input.AccountPracticeId; item.MeetingDate = input.MeetingDate; item.Attendees = input.Attendees; item.DiscussionSummary = input.DiscussionSummary; item.Decisions = input.Decisions; item.NextSteps = input.NextSteps; item.NextFollowUpDate = input.NextFollowUpDate;
        if (input.Id == 0) db.Meetings.Add(item); await db.SaveChangesAsync(cancellationToken);
        await db.MeetingWorkItems.Where(x => x.MeetingId == item.Id).ExecuteDeleteAsync(cancellationToken);
        db.MeetingWorkItems.AddRange(input.SelectedWorkItemIds.Distinct().Select(id => new MeetingWorkItem { MeetingId = item.Id, WorkItemId = id })); await db.SaveChangesAsync(cancellationToken); return item.Id;
    }

    public async Task<List<ReleasePlan>> GetReleasePlansAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ReleasePlans
            .Include(x => x.ReleaseWorkItems)
            .ThenInclude(x => x.WorkItem)
            .ThenInclude(x => x.WorkItemAccounts)
            .ThenInclude(x => x.AccountPractice)
            .AsNoTracking()
            .OrderBy(x => x.Status == "Released" || x.Status == "Completed" ? 1 : 0)
            .ThenBy(x => x.ReleaseDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleasePlan?> GetReleasePlanAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var release = await db.ReleasePlans
            .Include(x => x.ReleaseWorkItems)
            .ThenInclude(x => x.WorkItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (release != null) release.SelectedWorkItemIds = release.ReleaseWorkItems.Select(x => x.WorkItemId).ToList();
        return release;
    }

    public async Task<List<WorkItem>> GetReleaseCandidateTasksAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await TaskGraph(db.WorkItems)
            .Where(x => !ArchivedStatuses.Contains(x.Status))
            .Where(x =>
                ReleaseCandidateStatuses.Contains(x.Status) ||
                x.Type == "Deployment" ||
                !string.IsNullOrWhiteSpace(x.ReleaseVersion) ||
                x.ReleaseWorkItems.Any(r => ActiveReleaseStatuses.Contains(r.ReleasePlan.Status)))
            .OrderBy(x => ReleaseCandidateStatuses.Contains(x.Status) ? 0 : 1)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SaveReleasePlanAsync(ReleasePlan input, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = input.Id == 0 ? new ReleasePlan() : await db.ReleasePlans.FindAsync([input.Id], cancellationToken) ?? throw new InvalidOperationException("Release plan not found.");
        item.Version = input.Version.Trim();
        if (string.IsNullOrWhiteSpace(item.Version)) throw new InvalidOperationException("Release version is required.");
        item.Status = input.Status;
        item.ReleaseDate = input.ReleaseDate;
        item.ReleaseNotes = input.ReleaseNotes;
        item.QaSignedOff = input.QaSignedOff;
        item.ClientSignedOff = input.ClientSignedOff;

        if (input.Id == 0) db.ReleasePlans.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        await db.ReleaseWorkItems.Where(x => x.ReleasePlanId == item.Id).ExecuteDeleteAsync(cancellationToken);
        db.ReleaseWorkItems.AddRange(input.SelectedWorkItemIds.Distinct().Select(id => new ReleaseWorkItem { ReleasePlanId = item.Id, WorkItemId = id }));
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    private static IQueryable<WorkItem> TaskGraph(IQueryable<WorkItem> query) => query
        .Include(x => x.AccountPractice)
        .Include(x => x.AssignedDeveloper)
        .Include(x => x.AssignedQa)
        .Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice)
        .Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember)
        .Include(x => x.ReleaseWorkItems).ThenInclude(x => x.ReleasePlan);

    private static void CopyTask(WorkItem source, WorkItem target)
    {
        target.Title = source.Title; target.Description = source.Description; target.ModuleName = source.ModuleName; target.Type = source.Type; target.Priority = source.Priority; target.Status = source.Status; target.AssignedQaId = source.AssignedQaId; target.OwnerId = source.OwnerId; target.DueDate = source.DueDate; target.ReleaseVersion = source.ReleaseVersion; target.LatestUpdate = source.LatestUpdate; target.BlockerReason = source.BlockerReason; target.IsClientVisible = source.IsClientVisible;
    }

    private static string AccountPriority(IReadOnlyCollection<WorkItem> tasks)
    {
        if (tasks.Any(task => task.Priority is "Critical" or "High")) return "High";
        if (tasks.Any(task => task.Priority == "Medium")) return "Medium";
        return "Low";
    }

    private static IEnumerable<string> TaskAccountNames(WorkItem task)
    {
        if (task.AccountPractice != null) yield return task.AccountPractice.Name;
        foreach (var account in task.WorkItemAccounts.Select(link => link.AccountPractice).Where(account => account != null))
        {
            yield return account!.Name;
        }
    }
}
