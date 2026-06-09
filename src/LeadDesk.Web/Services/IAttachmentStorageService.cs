using Microsoft.AspNetCore.Http;

namespace LeadDesk.Web.Services;

public interface IAttachmentStorageService
{
    Task<StoredAttachment> SaveAsync(long workItemId, IFormFile file, CancellationToken cancellationToken = default);
    string GetDownloadUrl(string storageProvider, string storagePath);
}
