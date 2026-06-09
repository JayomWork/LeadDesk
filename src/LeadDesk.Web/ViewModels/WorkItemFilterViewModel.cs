using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LeadDesk.Web.ViewModels;

public class WorkItemFilterViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public long? AccountPracticeId { get; set; }
    public long? AssignedDeveloperId { get; set; }
    public bool IncludeCompleted { get; set; }
    public List<WorkItem> Items { get; set; } = [];
    public SelectList? Accounts { get; set; }
    public SelectList? TeamMembers { get; set; }
}
