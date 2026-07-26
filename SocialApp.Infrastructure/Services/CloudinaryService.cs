using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Common.Helpers;
using SocialApp.Application.DTOs.Cloud;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using SocialApp.Domain.Enums;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Implementation của ICloudinaryService dùng CloudinaryDotNet SDK.
/// Đặt ở Infrastructure layer — Application layer chỉ biết interface.
/// </summary>
public sealed class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinarySettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CloudinaryService> _logger;

    private static readonly string[] AllowedImageContentTypes =
        ["image/jpeg", "image/png", "image/gif", "image/webp"];

    private static readonly string[] AllowedVideoContentTypes =
        ["video/mp4", "video/webm", "video/quicktime"];

    private static readonly TimeSpan ImageUploadTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan VideoUploadTimeout = TimeSpan.FromMinutes(5);
    private const long MaxImageSizeBytes = 10L * 1024 * 1024;   // 10 MB
    private const long MaxVideoSizeBytes = 200L * 1024 * 1024;  // 200 MB

    public CloudinaryService(
        IOptions<CloudinarySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<CloudinaryService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var account = new Account(
            _settings.CloudName,
            _settings.ApiKey,
            _settings.ApiSecret);

        _cloudinary = new Cloudinary(account)
        {
            Api = { Secure = true }
        };
    }

    // UploadImageAsync

    public async Task<CloudUploadResult> UploadImageAsync(
        IFormFile file,
        string folder,
        int? maxWidthPx = null,
        int? qualityPercent = null)
    {
        var fileName = file?.FileName ?? "unknown";

        try
        {
            ValidateFileNotEmpty(file, nameof(file));
            ValidateContentType(file!, AllowedImageContentTypes,
                "Ảnh phải có định dạng: JPEG, PNG, GIF hoặc WEBP.");
            await ValidateImageMagicBytesAsync(file!);

            if (file!.Length > MaxImageSizeBytes)
                throw new ArgumentException("Ảnh không được vượt quá 10MB.");

            var sanitizedName = FileNameSanitizer.Sanitize(file.FileName);
            var fullFolder = BuildFolder(folder);

            await using var stream = file.OpenReadStream();

            var transformation = BuildImageTransformation(maxWidthPx, qualityPercent);

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(sanitizedName, stream),
                Folder = fullFolder,
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = false,
                Transformation = transformation
            };

            using var cts = new CancellationTokenSource(ImageUploadTimeout);
            var result = await _cloudinary.UploadAsync(uploadParams, cts.Token);

            if (result.Error is not null)
            {
                _logger.LogError(
                    "[CloudinaryService.UploadImageAsync] Upload thất bại — Folder: {Folder}, File: {File}, Error: {Err}",
                    fullFolder, sanitizedName, result.Error.Message);

                throw new InvalidOperationException(
                    $"Upload ảnh thất bại: {result.Error.Message}");
            }

            _logger.LogInformation(
                "[CloudinaryService.UploadImageAsync] Upload thành công — PublicId: {PubId}, Url: {Url}",
                result.PublicId, result.SecureUrl);

            return new CloudUploadResult
            {
                SecureUrl = result.SecureUrl.ToString(),
                PublicId = result.PublicId,
                FileSize = file.Length,
                Format = result.Format ?? Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
                Width = result.Width > 0 ? result.Width : null,
                Height = result.Height > 0 ? result.Height : null,
                StorageProvider = StorageProvider.Cloudinary,
                MediaType = MediaType.Image
            };
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CloudinaryService.UploadImageAsync] Exception không mong đợi — Folder: {Folder}, File: {File}",
                folder, fileName);
            throw new InvalidOperationException("Upload ảnh thất bại, vui lòng thử lại sau.", ex);
        }
    }

    // UploadVideoAsync

    public async Task<CloudUploadResult> UploadVideoAsync(
        IFormFile file,
        string folder)
    {
        var fileName = file?.FileName ?? "unknown";

        try
        {
            ValidateFileNotEmpty(file, nameof(file));
            ValidateContentType(file!, AllowedVideoContentTypes,
                "Video phải có định dạng: MP4, WEBM hoặc QuickTime (MOV).");
            await ValidateVideoMagicBytesAsync(file!);

            if (file!.Length > MaxVideoSizeBytes)
                throw new ArgumentException("Video không được vượt quá 200MB.");

            var sanitizedName = FileNameSanitizer.Sanitize(file.FileName);
            var fullFolder = BuildFolder(folder);

            await using var stream = file.OpenReadStream();

            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(sanitizedName, stream),
                Folder = fullFolder,
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = false
            };

            using var cts = new CancellationTokenSource(VideoUploadTimeout);
            var result = await _cloudinary.UploadAsync(uploadParams, cts.Token);

            if (result.Error is not null)
            {
                _logger.LogError(
                    "[CloudinaryService.UploadVideoAsync] Upload thất bại — Folder: {Folder}, File: {File}, Error: {Err}",
                    fullFolder, sanitizedName, result.Error.Message);

                throw new InvalidOperationException(
                    $"Upload video thất bại: {result.Error.Message}");
            }

            _logger.LogInformation(
                "[CloudinaryService.UploadVideoAsync] Upload thành công — PublicId: {PubId}, Url: {Url}",
                result.PublicId, result.SecureUrl);

            return new CloudUploadResult
            {
                SecureUrl = result.SecureUrl.ToString(),
                PublicId = result.PublicId,
                FileSize = file.Length,
                Format = result.Format ?? Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
                Width = result.Width > 0 ? result.Width : null,
                Height = result.Height > 0 ? result.Height : null,
                StorageProvider = StorageProvider.Cloudinary,
                MediaType = MediaType.Video
            };
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CloudinaryService.UploadVideoAsync] Exception không mong đợi — Folder: {Folder}, File: {File}",
                folder, fileName);
            throw new InvalidOperationException("Upload video thất bại, vui lòng thử lại sau.", ex);
        }
    }

    // DeleteAsync

    public async Task DeleteAsync(string publicId, ResourceType resourceType)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            _logger.LogWarning(
                "[CloudinaryService.DeleteAsync] PublicId rỗng — bỏ qua.");
            return;
        }

        try
        {
            var deletionParams = new DeletionParams(publicId)
            {
                ResourceType = resourceType
            };

            var result = await _cloudinary.DestroyAsync(deletionParams);

            if (result.Error is not null)
            {
                _logger.LogWarning(
                    "[CloudinaryService.DeleteAsync] Xóa thất bại — PublicId: {PubId}, ResourceType: {Type}, Error: {Err}",
                    publicId, resourceType, result.Error.Message);
            }
            else
            {
                _logger.LogInformation(
                    "[CloudinaryService.DeleteAsync] Đã xóa — PublicId: {PubId}, ResourceType: {Type}, Result: {Res}",
                    publicId, resourceType, result.Result);
            }
        }
        catch (Exception ex)
        {
            // Best-effort delete — không throw ra ngoài
            _logger.LogWarning(ex,
                "[CloudinaryService.DeleteAsync] Exception khi xóa — PublicId: {PubId}, ResourceType: {Type}",
                publicId, resourceType);
        }
    }

    // GetUsageMBAsync

    public async Task<double> GetUsageMBAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Cloudinary");

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.ApiSecret}"));

            var request = new HttpRequestMessage(
                 System.Net.Http.HttpMethod.Get,
                 $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/usage");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await client.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[CloudinaryService.GetUsageMBAsync] API trả về {Status}",
                    response.StatusCode);
                return 0;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("storage", out var storage) &&
                storage.TryGetProperty("usage", out var usageBytes) &&
                usageBytes.TryGetInt64(out var bytes))
            {
                var mb = bytes / 1024.0 / 1024.0;
                _logger.LogInformation(
                    "[CloudinaryService.GetUsageMBAsync] Dung lượng đã dùng: {MB:F2} MB", mb);
                return mb;
            }

            _logger.LogWarning(
                "[CloudinaryService.GetUsageMBAsync] Không parse được storage.usage từ response.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[CloudinaryService.GetUsageMBAsync] Exception khi gọi Cloudinary Usage API.");
            return 0;
        }
    }

    // Private helpers

    private static void ValidateFileNotEmpty(IFormFile? file, string paramName)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("File không được null hoặc rỗng.", paramName);
    }

    private static void ValidateContentType(
        IFormFile file,
        string[] allowedTypes,
        string message)
    {
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!allowedTypes.Contains(contentType))
            throw new ArgumentException(message);
    }

    private static async Task ValidateImageMagicBytesAsync(IFormFile file)
    {
        var header = await ReadHeaderBytesAsync(file, 12);
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

        var valid = contentType switch
        {
            "image/jpeg" => MagicBytesValidator.IsValidJpeg(header),
            "image/png" => MagicBytesValidator.IsValidPng(header),
            "image/gif" => MagicBytesValidator.IsValidGif(header),
            "image/webp" => MagicBytesValidator.IsValidWebp(header),
            _ => false
        };

        if (!valid)
            throw new ArgumentException("File không phải ảnh hợp lệ — magic bytes không khớp định dạng.");
    }

    private static async Task ValidateVideoMagicBytesAsync(IFormFile file)
    {
        var header = await ReadHeaderBytesAsync(file, 12);
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

        var valid = contentType switch
        {
            "video/mp4" => MagicBytesValidator.IsValidMp4(header),
            "video/webm" => MagicBytesValidator.IsValidWebm(header),
            "video/quicktime" => MagicBytesValidator.IsValidMp4(header)  // MOV dùng ftyp giống MP4
                              || IsValidMov(header),
            _ => false
        };

        if (!valid)
            throw new ArgumentException("File không phải video hợp lệ — magic bytes không khớp định dạng.");
    }

    private static bool IsValidMov(byte[] header)
    {
        if (header.Length < 8) return false;
        var marker = Encoding.ASCII.GetString(header, 4, 4);
        return marker is "wide" or "moov" or "ftyp";
    }

    private static async Task<byte[]> ReadHeaderBytesAsync(IFormFile file, int count)
    {
        var buffer = new byte[count];
        var stream = file.OpenReadStream();
        await using (stream.ConfigureAwait(false))
        {
            _ = await stream.ReadAsync(buffer.AsMemory(0, count));
        }

        // Reset stream để upload đọc lại từ đầu
        if (file.OpenReadStream().CanSeek)
        {
            var s = file.OpenReadStream();
            s.Position = 0;
        }

        return buffer;
    }

    private static Transformation BuildImageTransformation(int? maxWidthPx, int? qualityPercent)
    {
        var t = new Transformation();

        if (maxWidthPx.HasValue)
            t = t.Width(maxWidthPx.Value).Crop("limit");

        t = qualityPercent.HasValue
            ? t.Quality(qualityPercent.Value)
            : t.Quality("auto");

        return t.FetchFormat("auto");
    }

    private static string BuildFolder(string folder)
      => $"socialapp/{folder.Trim('/')}";
}