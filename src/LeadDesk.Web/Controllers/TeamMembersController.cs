using LeadDesk.Web.Data;
using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class TeamMembersController : Controller
{
    private readonly ApplicationDbContext _context;
    public TeamMembersController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.TeamMembers.OrderBy(x => x.FullName).ToListAsync());

    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public IActionResult Create() => View(new TeamMember());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create(TeamMember model)
    {
        if (!ModelState.IsValid) return View(model);
        _context.TeamMembers.Add(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Edit(long id)
    {
        var member = await _context.TeamMembers.FindAsync(id);
        return member == null ? NotFound() : View(member);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Edit(long id, TeamMember model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _context.TeamMembers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (existing == null) return NotFound();

        model.Uuid = existing.Uuid;
        model.CreatedAt = existing.CreatedAt;
        _context.Update(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
