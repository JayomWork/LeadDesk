using Microsoft.EntityFrameworkCore;
using ProjectPulse.Application;
using ProjectPulse.Domain;

namespace ProjectPulse.Infrastructure;

public sealed class LeadDeskService(IDbContextFactory<LeadDeskDbContext> contextFactory) : ILeadDeskService
{
    private static readonly string[] ArchivedStatuses = ["Completed", "Cancelled"];

    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var weekEnd = now.AddDays(7);
        return new DashboardSummary
        {
            TotalOpen = await db.WorkItems.CountAsync(x => x.Status != "Released" && !ArchivedStatuses.Contains(x.Status), cancellationToken),
            InDevelopment = await db.WorkItems.CountAsync(x => x.Status == "In Dev", cancellationToken),
            ReadyForQa = await db.WorkItems.CountAsync(x => x.Status == "Ready for QA", cancellationToken),
            Blocked = await db.WorkItems.CountAsync(x => x.Status == "Blocked", cancellationToken),
            DueThisWeek = await db.WorkItems.CountAsync(x => x.DueDate != null && x.DueDate <= weekEnd && x.Status != "Released" && !ArchivedStatuses.Contains(x.Status), cancellationToken),
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
            _ => items.OrderBy(x => x.Status == "Released" ? 0 : x.Status == "In Dev" ? 1 : x.Status == "Dev Done" ? 2 : x.Status == "In QA" ? 3 : x.Status == "New" ? 5 : 4).ThenByDescending(x => x.CreatedAt)
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

    private static IQueryable<WorkItem> TaskGraph(IQueryable<WorkItem> query) => query.Include(x => x.AccountPractice).Include(x => x.AssignedDeveloper).Include(x => x.AssignedQa).Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice).Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember);

    private static void CopyTask(WorkItem source, WorkItem target)
    {
        target.Title = source.Title; target.Description = source.Description; target.ModuleName = source.ModuleName; target.Type = source.Type; target.Priority = source.Priority; target.Status = source.Status; target.AssignedQaId = source.AssignedQaId; target.OwnerId = source.OwnerId; target.DueDate = source.DueDate; target.ReleaseVersion = source.ReleaseVersion; target.LatestUpdate = source.LatestUpdate; target.BlockerReason = source.BlockerReason; target.IsClientVisible = source.IsClientVisible;
    }
}
