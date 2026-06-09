using System.ComponentModel.DataAnnotations;

namespace LeadDesk.Web.Models;

public class Meeting
{
    public long Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MeetingType { get; set; } = "AccountCall";

    public long? AccountPracticeId { get; set; }
    public AccountPractice? AccountPractice { get; set; }

    public DateTime MeetingDate { get; set; } = DateTime.UtcNow;
    public string? Attendees { get; set; }
    public string? DiscussionSummary { get; set; }
    public string? Decisions { get; set; }
    public string? NextSteps { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MeetingWorkItem> MeetingWorkItems { get; set; } = new List<MeetingWorkItem>();
}
