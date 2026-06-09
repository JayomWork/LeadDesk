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
        var account = await _context.AccountPractices
            .Include(x => x.WorkItems)
            .FirstOrDefaultAsync(x => x.Id == id);
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
