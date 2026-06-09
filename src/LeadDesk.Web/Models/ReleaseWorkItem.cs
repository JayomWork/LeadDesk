namespace LeadDesk.Web.Models;

public class ReleaseWorkItem
{
    public long Id { get; set; }
    public long ReleasePlanId { get; set; }
    public ReleasePlan ReleasePlan { get; set; } = null!;
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
}
