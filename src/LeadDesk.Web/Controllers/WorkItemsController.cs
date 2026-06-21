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
    private static readonly string[] CompletedStatuses = ["Completed", "Cancelled"];
    private static readonly string[] Statuses = ["New", "Analysis", "Ready for Dev", "In Dev", "Dev Done", "Ready for QA", "In QA", "QA Done", "QA Failed", "Ready for UAT", "Ready for Release", "Released", "Completed", "Blocked", "On Hold", "Cancelled"];
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
            .Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember)
            .Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Title.Contains(filter.Search) || (x.Description != null && x.Description.Contains(filter.Search)));
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Priority)) query = query.Where(x => x.Priority == filter.Priority);
        if (filter.AccountPracticeId.HasValue) query = query.Where(x => x.AccountPracticeId == filter.AccountPracticeId || x.WorkItemAccounts.Any(a => a.AccountPracticeId == filter.AccountPracticeId));
        if (filter.AssignedDeveloperId.HasValue) query = query.Where(x => x.AssignedDeveloperId == filter.AssignedDeveloperId || x.WorkItemDevelopers.Any(d => d.TeamMemberId == filter.AssignedDeveloperId));
        if (!filter.IncludeCompleted && string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => !CompletedStatuses.Contains(x.Status));

        query = filter.SortBy switch
        {
            "Newest" => query.OrderByDescending(x => x.CreatedAt),
            "Oldest" => query.OrderBy(x => x.CreatedAt),
            "Title" => query.OrderBy(x => x.Title),
            "Priority" => query.OrderBy(x => x.Priority == "Critical" ? 0 : x.Priority == "High" ? 1 : x.Priority == "Medium" ? 2 : 3).ThenByDescending(x => x.CreatedAt),
            _ => query.OrderBy(x => x.Status == "Released" ? 0 : x.Status == "In Dev" ? 1 : x.Status == "Dev Done" ? 2 : x.Status == "In QA" ? 3 : x.Status == "New" ? 5 : 4).ThenByDescending(x => x.CreatedAt)
        };
        filter.Items = await query.ToListAsync();
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
            .Include(x => x.WorkItemDevelopers).ThenInclude(x => x.TeamMember)
            .Include(x => x.WorkItemAccounts).ThenInclude(x => x.AccountPractice)
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
        model.SelectedDeveloperIds = model.SelectedDeveloperIds.Distinct().ToList();
        model.SelectedAccountIds = model.SelectedAccountIds.Distinct().ToList();
        model.AssignedDeveloperId = model.SelectedDeveloperIds.FirstOrDefault() is var developerId && developerId > 0 ? developerId : null;
        model.AccountPracticeId = model.SelectedAccountIds.FirstOrDefault() is var accountId && accountId > 0 ? accountId : null;
        _context.WorkItems.Add(model);
        await _context.SaveChangesAsync();
        await ReplaceAssignments(model.Id, model.SelectedDeveloperIds, model.SelectedAccountIds);
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> Edit(long id)
    {
        var item = await _context.WorkItems
            .Include(x => x.WorkItemDevelopers)
            .Include(x => x.WorkItemAccounts)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();
        item.SelectedDeveloperIds = item.WorkItemDevelopers.Select(x => x.TeamMemberId).ToList();
        item.SelectedAccountIds = item.WorkItemAccounts.Select(x => x.AccountPracticeId).ToList();
        if (item.SelectedDeveloperIds.Count == 0 && item.AssignedDeveloperId.HasValue) item.SelectedDeveloperIds.Add(item.AssignedDeveloperId.Value);
        if (item.SelectedAccountIds.Count == 0 && item.AccountPracticeId.HasValue) item.SelectedAccountIds.Add(item.AccountPracticeId.Value);
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
        model.SelectedDeveloperIds = model.SelectedDeveloperIds.Distinct().ToList();
        model.SelectedAccountIds = model.SelectedAccountIds.Distinct().ToList();
        model.AssignedDeveloperId = model.SelectedDeveloperIds.FirstOrDefault() is var developerId && developerId > 0 ? developerId : null;
        model.AccountPracticeId = model.SelectedAccountIds.FirstOrDefault() is var accountId && accountId > 0 ? accountId : null;
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
        await ReplaceAssignments(model.Id, model.SelectedDeveloperIds, model.SelectedAccountIds);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> UpdateStatus(long id, string status)
    {
        if (!Statuses.Contains(status)) return BadRequest(new { message = "Invalid status." });
        var item = await _context.WorkItems.FindAsync(id);
        if (item == null) return NotFound();

        var oldStatus = item.Status;
        if (oldStatus != status)
        {
            item.Status = status;
            item.LatestUpdate = $"Status changed from {oldStatus} to {status}.";
            item.UpdatedAt = DateTime.UtcNow;
            _context.WorkItemUpdates.Add(new WorkItemUpdate { WorkItemId = id, OldStatus = oldStatus, NewStatus = status, UpdateText = item.LatestUpdate });
            await _context.SaveChangesAsync();
        }

        return Json(new { success = true, status });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles="Admin,ProjectLead,Manager,Developer,SrDeveloper,QA")]
    public async Task<IActionResult> Complete(long id)
    {
        var item = await _context.WorkItems.FindAsync(id);
        if (item == null) return NotFound();

        var oldStatus = item.Status;
        item.Status = "Completed";
        item.LatestUpdate = "Released task completed and archived.";
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
        ViewBag.Statuses = new SelectList(Statuses);
        ViewBag.Priorities = new SelectList(new[] { "Low", "Medium", "High", "Critical" });
        ViewBag.Types = new SelectList(new[] { "Requirement", "Bug", "Enhancement", "Support", "QA Issue", "Client Feedback", "Internal Task", "Research", "Deployment" });
    }

    private async Task ReplaceAssignments(long workItemId, IEnumerable<long> developerIds, IEnumerable<long> accountIds)
    {
        await _context.WorkItemDevelopers.Where(x => x.WorkItemId == workItemId).ExecuteDeleteAsync();
        await _context.WorkItemAccounts.Where(x => x.WorkItemId == workItemId).ExecuteDeleteAsync();
        _context.WorkItemDevelopers.AddRange(developerIds.Distinct().Select(id => new WorkItemDeveloper { WorkItemId = workItemId, TeamMemberId = id }));
        _context.WorkItemAccounts.AddRange(accountIds.Distinct().Select(id => new WorkItemAccount { WorkItemId = workItemId, AccountPracticeId = id }));
        await _context.SaveChangesAsync();
    }
}
