using ProjectPulse.Domain;

namespace ProjectPulse.Application;

public static class LeadDeskOptions
{
    public static readonly string[] Statuses = ["New", "Analysis", "Ready for Dev", "In Dev", "Dev Done", "Ready for QA", "In QA", "QA Done", "QA Failed", "Ready for UAT", "Ready for Release", "Released", "Completed", "Blocked", "On Hold", "Cancelled"];
    public static readonly string[] Priorities = ["Low", "Medium", "High", "Critical"];
    public static readonly string[] Types = ["Requirement", "Bug", "Enhancement", "Support", "QA Issue", "Client Feedback", "Internal Task", "Research", "Deployment"];
    public static readonly string[] Roles = ["Project Lead", "Manager", "Developer", "Sr Developer", "QA", "Client"];
    public static readonly string[] MeetingTypes = ["Standup", "Account Call", "Client Call", "Manager Meeting", "Internal Team Meeting", "Release Planning", "Requirement Discussion"];
}

public sealed class TaskQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public long? AccountId { get; set; }
    public long? DeveloperId { get; set; }
    public bool IncludeArchived { get; set; }
    public string SortBy { get; set; } = "Workflow";
}

public sealed class DashboardSummary
{
    public int TotalOpen { get; set; }
    public int InDevelopment { get; set; }
    public int ReadyForQa { get; set; }
    public int Blocked { get; set; }
    public int DueThisWeek { get; set; }
    public int ReleasedThisMonth { get; set; }
    public List<WorkItem> RecentItems { get; set; } = [];
}

public interface ILeadDeskService
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<List<WorkItem>> GetTasksAsync(TaskQuery query, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetTaskAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveTaskAsync(WorkItem item, CancellationToken cancellationToken = default);
    Task UpdateTaskStatusAsync(long id, string status, CancellationToken cancellationToken = default);
    Task ArchiveTaskAsync(long id, CancellationToken cancellationToken = default);
    Task<List<AccountPractice>> GetAccountsAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<AccountPractice?> GetAccountAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveAccountAsync(AccountPractice account, CancellationToken cancellationToken = default);
    Task<List<TeamMember>> GetTeamAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<TeamMember?> GetTeamMemberAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveTeamMemberAsync(TeamMember member, CancellationToken cancellationToken = default);
    Task<List<Meeting>> GetMeetingsAsync(CancellationToken cancellationToken = default);
    Task<Meeting?> GetMeetingAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveMeetingAsync(Meeting meeting, CancellationToken cancellationToken = default);
}
