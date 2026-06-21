using LeadDesk.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    public ReportsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Standup()
    {
        var items = await _context.WorkItems
            .Include(x => x.AccountPractice)
            .Include(x => x.AssignedDeveloper)
            .Include(x => x.AssignedQa)
            .Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice)
            .Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember)
            .Where(x => x.Status != "Released" && x.Status != "Completed" && x.Status != "Cancelled")
            .OrderBy(x => x.AssignedDeveloper!.FullName)
            .ThenBy(x => x.DueDate)
            .ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> AccountProgress(long? accountId)
    {
        ViewBag.Accounts = await _context.AccountPractices.OrderBy(x => x.Name).ToListAsync();
        ViewBag.SelectedAccountId = accountId;
        var query = _context.WorkItems.Include(x => x.AccountPractice).Include(x => x.AssignedDeveloper).Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice).Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember).AsQueryable();
        if (accountId.HasValue) query = query.Where(x => x.AccountPracticeId == accountId.Value || x.WorkItemAccounts.Any(a => a.AccountPracticeId == accountId.Value));
        var items = await query.OrderBy(x => x.Status).ThenByDescending(x => x.Priority).ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> ManagerSummary()
    {
        var items = await _context.WorkItems
            .Include(x => x.AccountPractice)
            .Include(x => x.AssignedDeveloper)
            .Include(x => x.AssignedQa)
            .Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice)
            .Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember)
            .Where(x => x.Status != "Released" && x.Status != "Completed" && x.Status != "Cancelled")
            .OrderBy(x => x.AssignedDeveloper!.FullName)
            .ToListAsync();
        return View(items);
    }
}
