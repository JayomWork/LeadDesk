namespace LeadDesk.Web.Models;

public class WorkItemAccount
{
    public long WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public long AccountPracticeId { get; set; }
    public AccountPractice AccountPractice { get; set; } = null!;
}
