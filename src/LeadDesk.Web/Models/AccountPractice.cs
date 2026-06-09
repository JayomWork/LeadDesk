using System.ComponentModel.DataAnnotations;

namespace LeadDesk.Web.Models;

public class AccountPractice
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ClientContactName { get; set; }

    [MaxLength(200), EmailAddress]
    public string? ClientContactEmail { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Active";

    public DateTime? GoLiveDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
