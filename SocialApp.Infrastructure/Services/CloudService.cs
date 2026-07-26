using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SocialApp.Application.Common.Helpers;
using SocialApp.Application.DTOs.Cloud;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Domain.Enums;

namespace SocialApp.Infrastructure.Services;

public sealed class CloudService : ICloudService
{
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IR2Service _r2Service;
    private readonly ILogger<CloudService> _logger;

    private static readonly string[] ImageContentTypes =
        ["image/jpeg", "image/png", "image/gif", "image/webp"];

    private static readonly string[] VideoContentTypes =
        ["video/mp4", "video/webm", "video/quicktime"];

    private static readonly string[] AudioContentTypes =
        ["audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4"];

    private const long MaxFileSizeBytes = 500L * 1024 * 1024; // 500 MB
    private const int MaxUploadCount = 10;
    private const int ImageMaxWidthPx = 1920;
    private const int ImageQualityPercent = 85;

    public CloudService(
        ICloudinaryService cloudinaryService,
        IR2Service r2Service,
        ILogger<CloudService> logger)
    {
        _cloudinaryService = cloudinaryService;
        _r2Service = r2Service;
        _logger = logger;
    }

    // IsImage / IsVideo / IsAudio

    public bool IsImage(IFormFile file)
    {
        var ct = file?.ContentType?.ToLowerInvariant() ?? string.Empty;
        return ImageContentTypes.Contains(ct);
    }

    public bool IsVideo(IFormFile file)
    {
        var ct = file?.ContentType?.ToLowerInvariant() ?? string.Empty;
        return VideoContentTypes.Contains(ct);
    }

    public bool IsAudio(IFormFile file)
    {
        var ct = file?.ContentType?.ToLowerInvariant() ?? string.Empty;
        return AudioContentTypes.Contains(ct);
    }

    // ValidateMagicBytesAsync

    public async Task<bool> ValidateMagicBytesAsync(IFormFile file)
    {
        if (file is null || file.Length == 0) return false;

        var buffer = new byte[12];
        var stream = file.OpenReadStream();

        await using (stream.ConfigureAwait(false))
        {
            _ = await stream.ReadAsync(buffer.AsMemory(0, 12));
        }

        // Reset stream về đầu để upload đọc lại từ đầu
        var resetStream = file.OpenReadStream();
        if (resetStream.CanSeek)
            resetStream.Position = 0;

        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        return MagicBytesValidator.Validate(buffer, contentType);
    }

    // UploadMediaAsync

    public async Task<CloudUploadResult> UploadMediaAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        if (file.Length == 0)
            throw new ArgumentException("File rỗng.");

        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("File vượt quá giới hạn 500MB.");

        var isValid = await ValidateMagicBytesAsync(file);
        if (!isValid)
            throw new ArgumentException("File không hợp lệ hoặc bị giả mạo định dạng.");

        CloudUploadResult result;
        string provider;

        if (IsImage(file))
        {
            provider = "Cloudinary";
            result = await _cloudinaryService.UploadImageAsync(
                file, folder,
                maxWidthPx: ImageMaxWidthPx,
                qualityPercent: ImageQualityPercent);
        }
        else if (IsVideo(file))
        {
            provider = "R2";
            result = await _r2Service.UploadAsync(file, folder, cancellationToken: cancellationToken);
        }
        else if (IsAudio(file))
        {
            provider = "R2";
            result = await _r2Service.UploadAsync(file, folder, cancellationToken: cancellationToken);
        }
        else
        {
            throw new ArgumentException("Định dạng file không được hỗ trợ.");
        }

        _logger.LogInformation(
            "[CloudService.UploadMediaAsync] Upload thành công — Provider: {Provider}, Folder: {Folder}, Size: {Size} bytes",
            provider, folder, file.Length);

        return result;
    }

    // UploadMultipleAsync

    public async Task<List<CloudUploadResult>> UploadMultipleAsync(
     IList<IFormFile> files,
     string folder,
     CancellationToken cancellationToken = default)
    {
        if (files is null || files.Count == 0) return [];
        if (files.Count > MaxUploadCount)
            throw new ArgumentException($"Tối đa {MaxUploadCount} file mỗi lần upload.");

        var tasks = files
            .Select(f => UploadMediaAsync(f, folder, cancellationToken))
            .ToList();

        try
        {
            var results = await Task.WhenAll(tasks);
            return [.. results];
        }
        catch
        {
            // Lấy các task đã thành công để cleanup
            var successResults = tasks
                .Where(t => t.IsCompletedSuccessfully)
                .Select(t => t.Result)
                .ToList();

            if (successResults.Count > 0)
            {
                _logger.LogWarning(
                    "[CloudService.UploadMultipleAsync] Một số file thất bại — cleanup {Count} file đã upload.",
                    successResults.Count);

                await Task.WhenAll(successResults.Select(r =>
                    DeleteMediaAsync(r.PublicId, r.StorageProvider, r.MediaType)));
            }

            var exceptions = tasks
                .Where(t => t.IsFaulted)
                .SelectMany(t => t.Exception!.InnerExceptions)
                .ToList();

            throw new AggregateException(
                "Một hoặc nhiều file upload thất bại. Các file đã upload thành công đã được xóa.",
                exceptions);
        }
    }

    // DeleteMediaAsync

    public async Task DeleteMediaAsync(
        string publicIdOrKey,
        StorageProvider provider,
        MediaType mediaType)
    {
        if (string.IsNullOrWhiteSpace(publicIdOrKey))
        {
            _logger.LogWarning(
                "[CloudService.DeleteMediaAsync] publicIdOrKey rỗng — bỏ qua.");
            return;
        }

        switch (provider)
        {
            case StorageProvider.Cloudinary:
                var resourceType = mediaType == MediaType.Image
                    ? ResourceType.Image
                    : ResourceType.Video; // Cloudinary dùng Video cho cả video lẫn audio

                await _cloudinaryService.DeleteAsync(publicIdOrKey, resourceType);

                _logger.LogInformation(
                    "[CloudService.DeleteMediaAsync] Đã xóa Cloudinary — PublicId: {Id}, ResourceType: {Type}",
                    publicIdOrKey, resourceType);
                break;

            case StorageProvider.R2:
                await _r2Service.DeleteAsync(publicIdOrKey);

                _logger.LogInformation(
                    "[CloudService.DeleteMediaAsync] Đã xóa R2 — Key: {Key}",
                    publicIdOrKey);
                break;

            default:
                _logger.LogWarning(
                    "[CloudService.DeleteMediaAsync] Provider không xác định: {Provider}", provider);
                break;
        }
    }

    // GetStatsAsync

    public async Task<AdminCloudStatsDto> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var cloudinaryUsageTask = _cloudinaryService.GetUsageMBAsync();
        var r2StatsTask = _r2Service.GetStorageStatsAsync(cancellationToken);
        var recentFilesTask = _r2Service.ListFilesAsync(null, 20, cancellationToken);

        await Task.WhenAll(cloudinaryUsageTask, r2StatsTask, recentFilesTask);

        // Nếu Cloudinary thất bại → dùng 0, không fail toàn bộ
        var cloudinaryUsageMb = cloudinaryUsageTask.IsCompletedSuccessfully
            ? cloudinaryUsageTask.Result
            : 0;

        if (!cloudinaryUsageTask.IsCompletedSuccessfully)
        {
            _logger.LogWarning(
                "[CloudService.GetStatsAsync] Không lấy được Cloudinary usage — dùng 0.");
        }

        return new AdminCloudStatsDto
        {
            CloudinaryUsageMB = cloudinaryUsageMb,
            CloudinaryPlanLimitMB = 25600, // 25GB free plan
            R2Stats = r2StatsTask.Result,
            RecentR2Files = recentFilesTask.Result.ToList()
        };
    }
}