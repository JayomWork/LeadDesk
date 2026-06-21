using LeadDesk.Web.Data;
using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class ReleasePlansController : Controller
{
    private static readonly string[] EligibleStatuses = ["Dev Done", "Ready for QA", "In QA", "QA Done", "Ready for Release"];
    private readonly ApplicationDbContext _context;
    public ReleasePlansController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var releases = await _context.ReleasePlans
            .Include(x => x.ReleaseWorkItems).ThenInclude(x => x.WorkItem)
            .OrderBy(x => x.Status == "Released" ? 1 : 0)
            .ThenByDescending(x => x.ReleaseDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
        return View(releases);
    }

    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create()
    {
        var model = new ReleasePlan { Status = "Planned", ReleaseDate = DateTime.Today };
        await PopulateTasks(model.SelectedWorkItemIds);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create(ReleasePlan model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateTasks(model.SelectedWorkItemIds);
            return View(model);
        }

        _context.ReleasePlans.Add(model);
        await _context.SaveChangesAsync();
        await ReplaceTasks(model.Id, model.SelectedWorkItemIds);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Edit(long id)
    {
        var model = await _context.ReleasePlans.Include(x => x.ReleaseWorkItems).FirstOrDefaultAsync(x => x.Id == id);
        if (model == null) return NotFound();
        model.SelectedWorkItemIds = model.ReleaseWorkItems.Select(x => x.WorkItemId).ToList();
        await PopulateTasks(model.SelectedWorkItemIds);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Edit(long id, ReleasePlan model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateTasks(model.SelectedWorkItemIds);
            return View(model);
        }

        var existing = await _context.ReleasePlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (existing == null) return NotFound();
        model.CreatedAt = existing.CreatedAt;
        _context.Update(model);
        await _context.SaveChangesAsync();
        await ReplaceTasks(id, model.SelectedWorkItemIds);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> MarkReleased(long id)
    {
        var release = await _context.ReleasePlans.Include(x => x.ReleaseWorkItems).ThenInclude(x => x.WorkItem).FirstOrDefaultAsync(x => x.Id == id);
        if (release == null) return NotFound();

        release.Status = "Released";
        release.ReleaseDate ??= DateTime.Today;
        foreach (var link in release.ReleaseWorkItems)
        {
            var task = link.WorkItem;
            var oldStatus = task.Status;
            if (oldStatus == "Released") continue;
            task.Status = "Released";
            task.ReleaseVersion = release.Version;
            task.LatestUpdate = $"Released in {release.Version}.";
            task.UpdatedAt = DateTime.UtcNow;
            _context.WorkItemUpdates.Add(new WorkItemUpdate { WorkItemId = task.Id, OldStatus = oldStatus, NewStatus = "Released", UpdateText = task.LatestUpdate });
        }
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateTasks(IEnumerable<long> selectedIds)
    {
        var selected = selectedIds.Distinct().ToList();
        var tasks = await _context.WorkItems
            .Where(x => EligibleStatuses.Contains(x.Status) || selected.Contains(x.Id))
            .OrderBy(x => x.Status == "Ready for Release" ? 0 : x.Status == "QA Done" ? 1 : x.Status == "In QA" ? 2 : 3)
            .ThenBy(x => x.Title)
            .Select(x => new { x.Id, Label = x.Title + " [" + x.Status + "]" })
            .ToListAsync();
        ViewBag.EligibleTasks = new MultiSelectList(tasks, "Id", "Label", selected);
    }

    private async Task ReplaceTasks(long releasePlanId, IEnumerable<long> workItemIds)
    {
        await _context.ReleaseWorkItems.Where(x => x.ReleasePlanId == releasePlanId).ExecuteDeleteAsync();
        _context.ReleaseWorkItems.AddRange(workItemIds.Distinct().Select(id => new ReleaseWorkItem { ReleasePlanId = releasePlanId, WorkItemId = id }));
        await _context.SaveChangesAsync();
    }
}
