namespace LeadDesk.Web.Models;

public class MeetingWorkItem
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
}
