using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace LeadDesk.Web.Services;

public class AttachmentStorageService : IAttachmentStorageService
{
    private readonly AttachmentStorageOptions _options;
    private readonly IWebHostEnvironment _environment;

    public AttachmentStorageService(IOptions<AttachmentStorageOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<StoredAttachment> SaveAsync(long workItemId, IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        var safeFileName = Path.GetFileName(file.FileName);
        var storageName = $"{Guid.NewGuid()}{extension}";

        if (UseS3())
        {
            var keyPrefix = _options.S3KeyPrefix.Trim('/');
            var key = $"{keyPrefix}/{workItemId}/{storageName}";
            using var client = CreateS3Client();
            var request = new PutObjectRequest
            {
                BucketName = _options.S3BucketName,
                CannedACL = S3CannedACL.Private,
                Key = key,
                InputStream = file.OpenReadStream(),
                ContentType = file.ContentType
            };

            await client.PutObjectAsync(request, cancellationToken);
            return new StoredAttachment("S3", key, safeFileName, file.ContentType, file.Length);
        }

        var relativeRoot = _options.LocalRootPath.Trim('/', '\\');
        var relativePath = Path.Combine(relativeRoot, workItemId.ToString(), storageName).Replace('\\', '/');
        var absolutePath = Path.Combine(_environment.WebRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        return new StoredAttachment("Local", relativePath, safeFileName, file.ContentType, file.Length);
    }

    public string GetDownloadUrl(string storageProvider, string storagePath)
    {
        if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase) && UseS3())
        {
            using var client = CreateS3Client();
            return client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _options.S3BucketName,
                Key = storagePath,
                Expires = DateTime.UtcNow.AddMinutes(_options.DownloadUrlExpiryMinutes)
            });
        }

        return "/" + storagePath.TrimStart('/', '\\').Replace('\\', '/');
    }

    private bool UseS3() =>
        _options.Provider.Equals("S3", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_options.S3BucketName);

    private AmazonS3Client CreateS3Client()
    {
        var region = RegionEndpoint.GetBySystemName(_options.S3Region);
        if (!string.IsNullOrWhiteSpace(_options.S3AccessKey) && !string.IsNullOrWhiteSpace(_options.S3SecretKey))
        {
            return new AmazonS3Client(_options.S3AccessKey, _options.S3SecretKey, region);
        }

        return new AmazonS3Client(region);
    }
}
