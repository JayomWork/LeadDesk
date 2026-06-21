using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeadDesk.Web.Models;

public class ReleasePlan
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Version { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "Planned";

    public DateTime? ReleaseDate { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool QaSignedOff { get; set; }
    public bool ClientSignedOff { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReleaseWorkItem> ReleaseWorkItems { get; set; } = new List<ReleaseWorkItem>();

    [NotMapped]
    public List<long> SelectedWorkItemIds { get; set; } = [];
}
