using ProjectPulse.Domain;

namespace ProjectPulse.Application;

public static class LeadDeskOptions
{
    public static readonly string[] Statuses = ["New", "Analysis", "Ready for Dev", "In Dev", "Dev Done", "Ready for QA", "In QA", "QA Done", "QA Failed", "Ready for UAT", "Ready for Release", "Released", "Completed", "Blocked", "On Hold", "Cancelled"];
    public static readonly string[] Priorities = ["Low", "Medium", "High", "Critical"];
    public static readonly string[] Types = ["Requirement", "Bug", "Enhancement", "Support", "QA Issue", "Client Feedback", "Internal Task", "Research", "Deployment"];
    public static readonly string[] Roles = ["Project Lead", "Manager", "Developer", "Sr Developer", "QA", "Client"];
    public static readonly string[] MeetingTypes = ["Standup", "Account Call", "Client Call", "Manager Meeting", "Internal Team Meeting", "Release Planning", "Requirement Discussion"];
    public static readonly string[] ReleaseStatuses = ["Planned", "In Progress", "Ready", "Released", "Completed", "Cancelled"];
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

public sealed class AccountTaskSummary
{
    public AccountPractice Account { get; set; } = new();
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int InQueueTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int QaTasks { get; set; }
    public int ReleaseReadyTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int BlockedTasks { get; set; }
    public int DoneTasks { get; set; }
    public string OwnerName { get; set; } = "Unassigned";
    public string Priority { get; set; } = "Low";
    public DateTime LastUpdatedAt { get; set; }
    public DateTime? NextMeetingDate { get; set; }
    public List<string> TaskStatuses { get; set; } = [];
}

public sealed class TeamMemberTaskSummary
{
    public TeamMember Member { get; set; } = new();
    public int TotalTasks { get; set; }
    public int PendingTaskCount { get; set; }
    public int InProgressTaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int OverdueTaskCount { get; set; }
    public int CompletionPercentage { get; set; }
    public string PrimaryAccountName { get; set; } = "Unassigned";
    public List<string> AssignedAccounts { get; set; } = [];
    public DateTime LastUpdatedAt { get; set; }
    public List<TeamMemberRecentTask> RecentTasks { get; set; } = [];
}

public sealed class TeamMemberRecentTask
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AccountName { get; set; } = "All accounts";
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsImportant { get; set; }
}

public sealed class AIWritingRequestDto
{
    public string FieldName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public AIWritingTaskContextDto TaskContext { get; set; } = new();
}

public sealed class AIWritingTaskContextDto
{
    public string? AccountName { get; set; }
    public string? Type { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
}

public sealed class AIWritingResponseDto
{
    public string ImprovedText { get; set; } = string.Empty;
}

public sealed class AIWritingOptions
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
}

public interface IAIWritingService
{
    Task<AIWritingResponseDto> ImproveTextAsync(AIWritingRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class BoardTaskUpdate
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Medium";
    public long? TeamMemberId { get; set; }
    public DateTime? DueDate { get; set; }
}

public interface ILeadDeskService
{
    event Action? ImportantTasksChanged;
    Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<List<WorkItem>> GetTasksAsync(TaskQuery query, CancellationToken cancellationToken = default);
    Task<WorkItem?> GetTaskAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveTaskAsync(WorkItem item, CancellationToken cancellationToken = default);
    Task UpdateTaskStatusAsync(long id, string status, CancellationToken cancellationToken = default);
    Task UpdateTaskAssigneeAsync(long id, long? teamMemberId, CancellationToken cancellationToken = default);
    Task UpdateBoardTaskAsync(BoardTaskUpdate update, CancellationToken cancellationToken = default);
    Task UpdateTaskImportanceAsync(long id, bool isImportant, CancellationToken cancellationToken = default);
    Task<List<WorkItem>> GetImportantTasksAsync(CancellationToken cancellationToken = default);
    Task ArchiveTaskAsync(long id, CancellationToken cancellationToken = default);
    Task<List<AccountPractice>> GetAccountsAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<List<AccountTaskSummary>> GetAccountTaskSummariesAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<AccountPractice?> GetAccountAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveAccountAsync(AccountPractice account, CancellationToken cancellationToken = default);
    Task<List<TeamMember>> GetTeamAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<List<TeamMemberTaskSummary>> GetTeamTaskSummariesAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<TeamMember?> GetTeamMemberAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveTeamMemberAsync(TeamMember member, CancellationToken cancellationToken = default);
    Task<List<Meeting>> GetMeetingsAsync(CancellationToken cancellationToken = default);
    Task<Meeting?> GetMeetingAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveMeetingAsync(Meeting meeting, CancellationToken cancellationToken = default);
    Task<List<ReleasePlan>> GetReleasePlansAsync(CancellationToken cancellationToken = default);
    Task<ReleasePlan?> GetReleasePlanAsync(long id, CancellationToken cancellationToken = default);
    Task<List<WorkItem>> GetReleaseCandidateTasksAsync(CancellationToken cancellationToken = default);
    Task<long> SaveReleasePlanAsync(ReleasePlan releasePlan, CancellationToken cancellationToken = default);
}
