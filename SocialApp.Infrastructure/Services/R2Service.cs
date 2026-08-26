using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Common.Helpers;
using SocialApp.Application.DTOs.Cloud;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using SocialApp.Domain.Enums;

namespace SocialApp.Infrastructure.Services;

public sealed class R2Service : IR2Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly CloudflareR2Settings _settings;
    private readonly ILogger<R2Service> _logger;

    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "video/mp4", "video/webm", "video/quicktime",
        "audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4", "audio/x-m4a"
    ];

    private const long MaxFileSizeBytes = 500L * 1024 * 1024; // 500 MB
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(5);

    public R2Service(
        IOptions<CloudflareR2Settings> settings,
        ILogger<R2Service> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var s3Config = new AmazonS3Config
        {
            ServiceURL = _settings.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        };

        _s3Client = new AmazonS3Client(
            _settings.AccessKeyId,
            _settings.SecretAccessKey,
            s3Config);
    }

    // UploadAsync

    public async Task<CloudUploadResult> UploadAsync(
        IFormFile file,
        string folder,
        string? customFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("File không được null hoặc rỗng.");

        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException(
                "Định dạng file không được hỗ trợ. Chỉ chấp nhận ảnh, video và audio.");

        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("File không được vượt quá 500MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var rawFileName = customFileName is not null
            ? FileNameSanitizer.Sanitize(customFileName)
            : $"{Guid.NewGuid()}{ext}";

        var sanitizedFileName = FileNameSanitizer.Sanitize(rawFileName);
        var key = $"{folder.Trim('/')}/{sanitizedFileName}";

        try
        {
            await using var stream = file.OpenReadStream();

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead,
                // R2 không hỗ trợ STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER
                // Phải disable payload signing để dùng UNSIGNED-PAYLOAD thay thế
                DisablePayloadSigning = true
            };

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(UploadTimeout);

            await _s3Client.PutObjectAsync(request, linkedCts.Token);

            var publicUrl = $"{_settings.PublicUrl.TrimEnd('/')}/{key}";
            var mediaType = DetectMediaType(contentType);

            _logger.LogInformation(
                "[R2Service.UploadAsync] Upload thành công — Key: {Key}, Size: {Size} bytes, Url: {Url}",
                key, file.Length, publicUrl);

            return new CloudUploadResult
            {
                SecureUrl = publicUrl,
                PublicId = key,
                FileSize = file.Length,
                Format = ext.TrimStart('.'),
                StorageProvider = StorageProvider.R2,
                MediaType = mediaType
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex,
                "[R2Service.UploadAsync] Upload bị hủy — Key: {Key}", key);
            throw;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex,
                "[R2Service.UploadAsync] S3 exception — Key: {Key}, StatusCode: {Code}",
                key, ex.StatusCode);
            throw new InvalidOperationException($"Upload file lên R2 thất bại: {ex.Message}", ex);
        }
    }

    // DeleteAsync

    public async Task DeleteAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("[R2Service.DeleteAsync] Key rỗng — bỏ qua.");
            return;
        }

        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key
            };

            // R2 trả 204 kể cả key không tồn tại — idempotent
            await _s3Client.DeleteObjectAsync(request);

            _logger.LogInformation(
                "[R2Service.DeleteAsync] Đã xóa — Key: {Key}", key);
        }
        catch (Exception ex)
        {
            // Best-effort delete — không throw ra ngoài
            _logger.LogWarning(ex,
                "[R2Service.DeleteAsync] Exception khi xóa — Key: {Key}", key);
        }
    }

    // ExistsAsync

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _settings.BucketName,
                Key = key
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when ((int)ex.StatusCode == 404)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex,
                "[R2Service.ExistsAsync] S3 exception — Key: {Key}, StatusCode: {Code}",
                key, ex.StatusCode);
            throw new InvalidOperationException($"Kiểm tra file thất bại: {ex.Message}", ex);
        }
    }

    // ListFilesAsync

    public async Task<IEnumerable<R2FileInfo>> ListFilesAsync(
        string? prefix = null,
        int maxKeys = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _settings.BucketName,
                Prefix = prefix,
                MaxKeys = maxKeys
            };

            var response = await _s3Client.ListObjectsV2Async(request, cancellationToken);

            // Guard null: S3Objects có thể null khi bucket rỗng hoặc prefix không match
            var files = (response.S3Objects ?? Enumerable.Empty<Amazon.S3.Model.S3Object>())
                .Select(obj =>
                {
                    var detectedContentType = DetectContentTypeFromExtension(obj.Key);

                    return new R2FileInfo
                    {
                        Key = obj.Key,
                        PublicUrl = $"{_settings.PublicUrl.TrimEnd('/')}/{obj.Key}",
                        FileName = Path.GetFileName(obj.Key),
                        ContentType = detectedContentType,
                        FileSize = obj.Size ?? 0,
                        LastModified = obj.LastModified?.ToUniversalTime() ?? DateTime.UtcNow
                    };
                })
                .OrderByDescending(f => f.LastModified);

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[R2Service.ListFilesAsync] Exception khi liệt kê file — Prefix: {Prefix}", prefix);
            throw;
        }
    }

    // GetStorageStatsAsync

    public async Task<R2StorageStats> GetStorageStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var files = (await ListFilesAsync(
            prefix: null,
            maxKeys: 1000,
            cancellationToken: cancellationToken)).ToList();

        var totalSizeBytes = files.Sum(f => f.FileSize);

        var filesByType = new Dictionary<string, int>
        {
            ["image"] = 0,
            ["video"] = 0,
            ["audio"] = 0,
            ["other"] = 0
        };

        foreach (var file in files)
        {
            var prefix = file.ContentType.Split('/').FirstOrDefault() ?? "other";
            var bucket = prefix switch
            {
                "image" => "image",
                "video" => "video",
                "audio" => "audio",
                _ => "other"
            };
            filesByType[bucket]++;
        }

        return new R2StorageStats
        {
            TotalFiles = files.Count,
            TotalSizeBytes = totalSizeBytes,
            FilesByType = filesByType
        };
    }

    // Private helpers

    private static MediaType DetectMediaType(string contentType) => contentType switch
    {
        var ct when ct.StartsWith("image/") => MediaType.Image,
        var ct when ct.StartsWith("video/") => MediaType.Video,
        var ct when ct.StartsWith("audio/") => MediaType.Audio,
        _ => MediaType.Image
    };

    private static string DetectContentTypeFromExtension(string key)
    {
        var ext = Path.GetExtension(key).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };
    }
}