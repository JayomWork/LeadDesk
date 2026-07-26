using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectPulse.Domain;

public class AccountPractice
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(150)] public string? ClientContactName { get; set; }
    [MaxLength(200), EmailAddress] public string? ClientContactEmail { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "Active";
    public DateTime? GoLiveDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<WorkItem> WorkItems { get; set; } = [];
}

public class TeamMember
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Role { get; set; } = "Developer";
    [MaxLength(200), EmailAddress] public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WorkItem
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();
    public long? AccountPracticeId { get; set; }
    public AccountPractice? AccountPractice { get; set; }
    [Required, MaxLength(250)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    [MaxLength(100)] public string ModuleName { get; set; } = string.Empty;
    [MaxLength(50)] public string Type { get; set; } = "Requirement";
    [MaxLength(30)] public string Priority { get; set; } = "Medium";
    [MaxLength(50)] public string Status { get; set; } = "New";
    public long? AssignedDeveloperId { get; set; }
    public TeamMember? AssignedDeveloper { get; set; }
    public long? AssignedQaId { get; set; }
    public TeamMember? AssignedQa { get; set; }
    public long? OwnerId { get; set; }
    public TeamMember? Owner { get; set; }
    public DateTime? DueDate { get; set; }
    [MaxLength(50)] public string? ReleaseVersion { get; set; }
    public string? LatestUpdate { get; set; }
    public string? BlockerReason { get; set; }
    public bool IsClientVisible { get; set; }
    public bool IsImportant { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<WorkItemUpdate> Updates { get; set; } = [];
    public ICollection<WorkItemAttachment> Attachments { get; set; } = [];
    public ICollection<MeetingWorkItem> MeetingWorkItems { get; set; } = [];
    public ICollection<ReleaseWorkItem> ReleaseWorkItems { get; set; } = [];
    public ICollection<WorkItemDeveloper> WorkItemDevelopers { get; set; } = [];
    public ICollection<WorkItemAccount> WorkItemAccounts { get; set; } = [];
    [NotMapped] public List<long> SelectedDeveloperIds { get; set; } = [];
    [NotMapped] public List<long> SelectedAccountIds { get; set; } = [];
}

public class WorkItemUpdate
{
    public long Id { get; set; }
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    [Required] public string UpdateText { get; set; } = string.Empty;
    [MaxLength(50)] public string? OldStatus { get; set; }
    [MaxLength(50)] public string? NewStatus { get; set; }
    public long? UpdatedById { get; set; }
    public TeamMember? UpdatedBy { get; set; }
    public bool IsInternalNote { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WorkItemAttachment
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();
    public long WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    [Required, MaxLength(260)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string StoragePath { get; set; } = string.Empty;
    [MaxLength(50)] public string StorageProvider { get; set; } = "Local";
    [MaxLength(150)] public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    [MaxLength(30)] public string SourceType { get; set; } = "Document";
    [MaxLength(250)] public string? EmailSubject { get; set; }
    [MaxLength(200)] public string? EmailFrom { get; set; }
    public DateTime? EmailReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Meeting
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(50)] public string MeetingType { get; set; } = "AccountCall";
    public long? AccountPracticeId { get; set; }
    public AccountPractice? AccountPractice { get; set; }
    public DateTime MeetingDate { get; set; } = DateTime.UtcNow;
    public string? Attendees { get; set; }
    public string? DiscussionSummary { get; set; }
    public string? Decisions { get; set; }
    public string? NextSteps { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<MeetingWorkItem> MeetingWorkItems { get; set; } = [];
    [NotMapped] public List<long> SelectedWorkItemIds { get; set; } = [];
}

public class MeetingWorkItem
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
}

public class ReleasePlan
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();
    [Required, MaxLength(100)] public string Version { get; set; } = string.Empty;
    [MaxLength(50)] public string Status { get; set; } = "Planned";
    public DateTime? ReleaseDate { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool QaSignedOff { get; set; }
    public bool ClientSignedOff { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ReleaseWorkItem> ReleaseWorkItems { get; set; } = [];
    [NotMapped] public List<long> SelectedWorkItemIds { get; set; } = [];
}

public class ReleaseWorkItem
{
    public long Id { get; set; }
    public long ReleasePlanId { get; set; }
    public ReleasePlan ReleasePlan { get; set; } = null!;
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public int SortOrder { get; set; }
}

public class WorkItemDeveloper
{
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public long TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;
}

public class WorkItemAccount
{
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public long AccountPracticeId { get; set; }
    public AccountPractice AccountPractice { get; set; } = null!;
}
