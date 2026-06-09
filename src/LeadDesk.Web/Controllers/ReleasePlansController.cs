using LeadDesk.Web.Data;
using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class ReleasePlansController : Controller
{
    private readonly ApplicationDbContext _context;
    public ReleasePlansController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.ReleasePlans.Include(x => x.ReleaseWorkItems).OrderByDescending(x => x.ReleaseDate).ToListAsync());

    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public IActionResult Create() => View(new ReleasePlan { Status = "Planned" });

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create(ReleasePlan model)
    {
        if (!ModelState.IsValid) return View(model);
        _context.ReleasePlans.Add(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
