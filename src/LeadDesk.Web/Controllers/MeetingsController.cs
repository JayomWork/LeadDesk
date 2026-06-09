using LeadDesk.Web.Data;
using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class MeetingsController : Controller
{
    private readonly ApplicationDbContext _context;
    public MeetingsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.Meetings.Include(x => x.AccountPractice).OrderByDescending(x => x.MeetingDate).ToListAsync());

    public async Task<IActionResult> Details(long id)
    {
        var meeting = await _context.Meetings
            .Include(x => x.AccountPractice)
            .Include(x => x.MeetingWorkItems).ThenInclude(x => x.WorkItem)
            .FirstOrDefaultAsync(x => x.Id == id);
        return meeting == null ? NotFound() : View(meeting);
    }

    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create()
    {
        await PopulateViewBags();
        return View(new Meeting { MeetingDate = DateTime.UtcNow });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create(Meeting model, long[] selectedWorkItemIds)
    {
        if (!ModelState.IsValid)
        {
            await PopulateViewBags();
            return View(model);
        }
        foreach (var id in selectedWorkItemIds.Distinct()) model.MeetingWorkItems.Add(new MeetingWorkItem { WorkItemId = id });
        _context.Meetings.Add(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private async Task PopulateViewBags()
    {
        ViewBag.Accounts = new SelectList(await _context.AccountPractices.OrderBy(x => x.Name).ToListAsync(), "Id", "Name");
        ViewBag.WorkItems = await _context.WorkItems.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        ViewBag.MeetingTypes = new SelectList(new[] { "Standup", "Account Call", "Client Call", "Manager Meeting", "Internal Team Meeting", "Release Planning", "Requirement Discussion" });
    }
}
