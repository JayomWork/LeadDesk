using LeadDesk.Web.Data;
using LeadDesk.Web.Models;
using LeadDesk.Web.Services;
using LeadDesk.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Controllers;

[Authorize]
public class WorkItemsController : Controller
{
    private static readonly string[] CompletedStatuses = ["Released", "Cancelled"];
    private readonly ApplicationDbContext _context;
    private readonly IAttachmentStorageService _attachmentStorage;
    public WorkItemsController(ApplicationDbContext context, IAttachmentStorageService attachmentStorage)
    {
        _context = context;
        _attachmentStorage = attachmentStorage;
    }

    public async Task<IActionResult> Index(WorkItemFilterViewModel filter)
    {
        var query = _context.WorkItems
            .Include(x => x.AccountPractice)
            .Include(x => x.AssignedDeveloper)
            .Include(x => x.AssignedQa)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Title.Contains(filter.Search) || (x.Description != null && x.Description.Contains(filter.Search)));
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Priority)) query = query.Where(x => x.Priority == filter.Priority);
        if (filter.AccountPracticeId.HasValue) query = query.Where(x => x.AccountPracticeId == filter.AccountPracticeId);
        if (filter.AssignedDeveloperId.HasValue) query = query.Where(x => x.AssignedDeveloperId == filter.AssignedDeveloperId);
        if (!filter.IncludeCompleted && string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => !CompletedStatuses.Contains(x.Status));

        filter.Items = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        await PopulateLists(filter);
        return View(filter);
    }

    public async Task<IActionResult> Details(long id)
    {
        var item = await _context.WorkItems
            .Include(x => x.AccountPractice)
            .Include(x => x.AssignedDeveloper)
            .Include(x => x.AssignedQa)
            .Include(x => x.Owner)
            .Include(x => x.Updates).ThenInclude(x => x.UpdatedBy)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id);
        return item == null ? NotFound() : View(item);
    }

    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create()
    {
        await PopulateViewBags();
        var defaultQa = await _context.TeamMembers
            .Where(x => x.IsActive && x.Role == "QA")
            .OrderBy(x => x.FullName)
            .FirstOrDefaultAsync();
        var defaultOwner = await _context.TeamMembers
            .Where(x => x.IsActive && x.Role == "Project Lead")
            .OrderBy(x => x.FullName)
            .FirstOrDefaultAsync();

        return View(new WorkItem
        {
            Status = "New",
            Priority = "Medium",
            Type = "Requirement",
            AssignedQaId = defaultQa?.Id,
            OwnerId = defaultOwner?.Id
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Create(WorkItem model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateViewBags();
            return View(model);
        }
        _context.WorkItems.Add(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> Edit(long id)
    {
        var item = await _context.WorkItems.FindAsync(id);
        if (item == null) return NotFound();
        await PopulateViewBags();
        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> Edit(long id, WorkItem model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateViewBags();
            return View(model);
        }

        var existing = await _context.WorkItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (existing == null) return NotFound();

        model.CreatedAt = existing.CreatedAt;
        model.UpdatedAt = DateTime.UtcNow;
        _context.Update(model);

        if (existing.Status != model.Status || existing.LatestUpdate != model.LatestUpdate)
        {
            _context.WorkItemUpdates.Add(new WorkItemUpdate
            {
                WorkItemId = model.Id,
                OldStatus = existing.Status,
                NewStatus = model.Status,
                UpdateText = string.IsNullOrWhiteSpace(model.LatestUpdate) ? $"Status changed from {existing.Status} to {model.Status}." : model.LatestUpdate
            });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> Complete(long id)
    {
        var item = await _context.WorkItems.FindAsync(id);
        if (item == null) return NotFound();

        var oldStatus = item.Status;
        item.Status = "Released";
        item.LatestUpdate = "Task completed and released.";
        item.UpdatedAt = DateTime.UtcNow;

        _context.WorkItemUpdates.Add(new WorkItemUpdate
        {
            WorkItemId = item.Id,
            OldStatus = oldStatus,
            NewStatus = item.Status,
            UpdateText = item.LatestUpdate
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager")]
    public async Task<IActionResult> Delete(long id)
    {
        var item = await _context.WorkItems.FindAsync(id);
        if (item == null) return NotFound();

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> AddUpdate(long workItemId, string updateText, string? newStatus, bool isInternalNote = true)
    {
        var item = await _context.WorkItems.FindAsync(workItemId);
        if (item == null) return NotFound();
        var oldStatus = item.Status;
        if (!string.IsNullOrWhiteSpace(newStatus)) item.Status = newStatus;
        item.LatestUpdate = updateText;
        item.UpdatedAt = DateTime.UtcNow;
        _context.WorkItemUpdates.Add(new WorkItemUpdate { WorkItemId = workItemId, UpdateText = updateText, OldStatus = oldStatus, NewStatus = item.Status, IsInternalNote = isInternalNote });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = workItemId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> AddAttachment(
        long workItemId,
        List<IFormFile> attachments,
        string sourceType = "Document",
        string? emailSubject = null,
        string? emailFrom = null,
        DateTime? emailReceivedAt = null)
    {
        var item = await _context.WorkItems.FindAsync(workItemId);
        if (item == null) return NotFound();

        var validFiles = (attachments ?? new List<IFormFile>()).Where(x => x.Length > 0).ToList();
        if (validFiles.Count == 0) return RedirectToAction(nameof(Details), new { id = workItemId });

        foreach (var file in validFiles)
        {
            var stored = await _attachmentStorage.SaveAsync(workItemId, file, HttpContext.RequestAborted);
            _context.WorkItemAttachments.Add(new WorkItemAttachment
            {
                WorkItemId = workItemId,
                FileName = stored.FileName,
                StorageProvider = stored.StorageProvider,
                StoragePath = stored.StoragePath,
                ContentType = stored.ContentType,
                FileSizeBytes = stored.FileSizeBytes,
                SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Document" : sourceType,
                EmailSubject = emailSubject,
                EmailFrom = emailFrom,
                EmailReceivedAt = emailReceivedAt
            });
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = workItemId });
    }

    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> DownloadAttachment(long id)
    {
        var attachment = await _context.WorkItemAttachments.FirstOrDefaultAsync(x => x.Id == id);
        if (attachment == null) return NotFound();

        return Redirect(_attachmentStorage.GetDownloadUrl(attachment.StorageProvider, attachment.StoragePath));
    }

    private async Task PopulateLists(WorkItemFilterViewModel filter)
    {
        filter.Accounts = new SelectList(await _context.AccountPractices.OrderBy(x => x.Name).ToListAsync(), "Id", "Name");
        filter.TeamMembers = new SelectList(await _context.TeamMembers.OrderBy(x => x.FullName).ToListAsync(), "Id", "FullName");
    }

    private async Task PopulateViewBags()
    {
        ViewBag.Accounts = new SelectList(await _context.AccountPractices.OrderBy(x => x.Name).ToListAsync(), "Id", "Name");
        ViewBag.TeamMembers = new SelectList(await _context.TeamMembers.Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync(), "Id", "FullName");
        ViewBag.DeveloperMembers = new SelectList(await _context.TeamMembers.Where(x => x.IsActive && (x.Role != "Client")).OrderBy(x => x.FullName).ToListAsync(), "Id", "FullName");
        ViewBag.QaMembers = new SelectList(await _context.TeamMembers.Where(x => x.IsActive && x.Role == "QA").OrderBy(x => x.FullName).ToListAsync(), "Id", "FullName");
        ViewBag.OwnerMembers = new SelectList(await _context.TeamMembers.Where(x => x.IsActive && (x.Role == "Client" || x.Role == "Project Lead" || x.Role == "Manager")).OrderBy(x => x.Role == "Project Lead" ? 0 : x.Role == "Manager" ? 1 : 2).ThenBy(x => x.FullName).ToListAsync(), "Id", "FullName");
        ViewBag.Statuses = new SelectList(new[] { "New", "Analysis", "Ready for Dev", "In Dev", "Dev Done", "Ready for QA", "In QA", "QA Failed", "Ready for UAT", "Ready for Release", "Released", "Blocked", "On Hold", "Cancelled" });
        ViewBag.Priorities = new SelectList(new[] { "Low", "Medium", "High", "Critical" });
        ViewBag.Types = new SelectList(new[] { "Requirement", "Bug", "Enhancement", "Support", "QA Issue", "Client Feedback", "Internal Task", "Research", "Deployment" });
    }
}
