using System.ComponentModel.DataAnnotations;

namespace LeadDesk.Web.Models;

public class TeamMember
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Role { get; set; } = "Developer";

    [MaxLength(200), EmailAddress]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
