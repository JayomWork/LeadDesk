using LeadDesk.Web.Data;
using LeadDesk.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    public DashboardController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var weekEnd = now.AddDays(7);
        var vm = new DashboardViewModel
        {
            TotalOpen = await _context.WorkItems.CountAsync(x => x.Status != "Released" && x.Status != "Cancelled"),
            InDevelopment = await _context.WorkItems.CountAsync(x => x.Status == "In Dev"),
            ReadyForQa = await _context.WorkItems.CountAsync(x => x.Status == "Ready for QA"),
            Blocked = await _context.WorkItems.CountAsync(x => x.Status == "Blocked"),
            DueThisWeek = await _context.WorkItems.CountAsync(x => x.DueDate != null && x.DueDate <= weekEnd && x.Status != "Released"),
            ReleasedThisMonth = await _context.WorkItems.CountAsync(x => x.Status == "Released" && x.UpdatedAt != null && x.UpdatedAt.Value.Month == now.Month),
            RecentItems = await _context.WorkItems.Include(x => x.AccountPractice).Include(x => x.AssignedDeveloper).Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice).Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember).OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).Take(8).ToListAsync(),
            BlockedItems = await _context.WorkItems.Include(x => x.AccountPractice).Include(x => x.AssignedDeveloper).Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice).Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember).Where(x => x.Status == "Blocked").Take(8).ToListAsync()
        };
        return View(vm);
    }
}
