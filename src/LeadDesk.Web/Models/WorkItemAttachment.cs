using System.ComponentModel.DataAnnotations;

namespace LeadDesk.Web.Models;

public class WorkItemAttachment
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    public long WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(50)]
    public string StorageProvider { get; set; } = "Local";

    [MaxLength(150)]
    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    [MaxLength(30)]
    public string SourceType { get; set; } = "Document";

    [MaxLength(250)]
    public string? EmailSubject { get; set; }

    [MaxLength(200)]
    public string? EmailFrom { get; set; }

    public DateTime? EmailReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
