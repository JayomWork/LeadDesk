using LeadDesk.Web.Models;

namespace LeadDesk.Web.ViewModels;

public class DashboardViewModel
{
    public int TotalOpen { get; set; }
    public int InDevelopment { get; set; }
    public int ReadyForQa { get; set; }
    public int Blocked { get; set; }
    public int DueThisWeek { get; set; }
    public int ReleasedThisMonth { get; set; }
    public List<WorkItem> RecentItems { get; set; } = [];
    public List<WorkItem> BlockedItems { get; set; } = [];
}
