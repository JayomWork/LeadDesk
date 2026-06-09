namespace LeadDesk.Web.Services;

public class AttachmentStorageOptions
{
    public string Provider { get; set; } = "Local";
    public string LocalRootPath { get; set; } = "uploads/work-items";
    public string S3BucketName { get; set; } = string.Empty;
    public string S3Region { get; set; } = "us-west-2";
    public string S3AccessKey { get; set; } = string.Empty;
    public string S3SecretKey { get; set; } = string.Empty;
    public string S3KeyPrefix { get; set; } = "ticket-attachments/";
    public int DownloadUrlExpiryMinutes { get; set; } = 10;
}
