namespace LeadDesk.Web.Services;

public record StoredAttachment(
    string StorageProvider,
    string StoragePath,
    string FileName,
    string? ContentType,
    long FileSizeBytes);
