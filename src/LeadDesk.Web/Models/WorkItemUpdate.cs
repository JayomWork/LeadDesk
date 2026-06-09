using System.ComponentModel.DataAnnotations;

namespace LeadDesk.Web.Models;

public class WorkItemUpdate
{
    public long Id { get; set; }

    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    [Required]
    public string UpdateText { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? OldStatus { get; set; }

    [MaxLength(50)]
    public string? NewStatus { get; set; }

    public long? UpdatedById { get; set; }
    public TeamMember? UpdatedBy { get; set; }

    public bool IsInternalNote { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
