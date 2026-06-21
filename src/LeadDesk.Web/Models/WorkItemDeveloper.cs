namespace LeadDesk.Web.Models;

public class WorkItemDeveloper
{
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public long TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;
}
