using LeadDesk.Web.Data;
using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class AccountsController : Controller
{
    private readonly ApplicationDbContext _context;
    public AccountsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.AccountPractices.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search));
        ViewBag.Search = search;
        return View(await query.OrderBy(x => x.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(long id)
    {
        var account = await _context.AccountPractices.FirstOrDefaultAsync(x => x.Id == id);
        if (account != null)
        {
            ViewBag.WorkItems = await _context.WorkItems
                .Include(x => x.AccountPractice).Include(x => x.AssignedDeveloper)
                .Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice)
                .Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember)
                .Where(x => x.AccountPracticeId == id || x.WorkItemAccounts.Any(a => a.AccountPracticeId == id))
                .OrderByDescending(x => x.CreatedAt).ToListAsync();
        }
        return account == null ? NotFound() : View(account);
    }

    [Authorize(Roles = "Admin,ProjectLead,Manager")]
    public IActionResult Create() => View(new AccountPractice());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create(AccountPractice model)
    {
        if (!ModelState.IsValid) return View(model);
        _context.AccountPractices.Add(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Edit(long id)
    {
        var account = await _context.AccountPractices.FindAsync(id);
        return account == null ? NotFound() : View(account);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Edit(long id, AccountPractice model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        model.UpdatedAt = DateTime.UtcNow;
        _context.Update(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
