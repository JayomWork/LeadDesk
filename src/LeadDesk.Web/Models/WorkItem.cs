using System.ComponentModel.DataAnnotations;

namespace LeadDesk.Web.Models;

public class WorkItem
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    public long? AccountPracticeId { get; set; }
    public AccountPractice? AccountPractice { get; set; }

    [Required, MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Type { get; set; } = "Requirement";

    [MaxLength(30)]
    public string Priority { get; set; } = "Medium";

    [MaxLength(50)]
    public string Status { get; set; } = "New";

    public long? AssignedDeveloperId { get; set; }
    public TeamMember? AssignedDeveloper { get; set; }

    public long? AssignedQaId { get; set; }
    public TeamMember? AssignedQa { get; set; }

    public long? OwnerId { get; set; }
    public TeamMember? Owner { get; set; }

    public DateTime? DueDate { get; set; }

    [MaxLength(50)]
    public string? ReleaseVersion { get; set; }

    public string? LatestUpdate { get; set; }
    public string? BlockerReason { get; set; }
    public bool IsClientVisible { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<WorkItemUpdate> Updates { get; set; } = new List<WorkItemUpdate>();
    public ICollection<WorkItemAttachment> Attachments { get; set; } = new List<WorkItemAttachment>();
    public ICollection<MeetingWorkItem> MeetingWorkItems { get; set; } = new List<MeetingWorkItem>();
    public ICollection<ReleaseWorkItem> ReleaseWorkItems { get; set; } = new List<ReleaseWorkItem>();
}
