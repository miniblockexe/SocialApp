using Microsoft.AspNetCore.Http;
using SocialApp.Application.DTOs.Cloud;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Façade kết hợp Cloudinary + R2 — caller không cần biết file đi đâu.
/// Routing logic: ảnh → Cloudinary, video/audio → R2.
/// Đây là interface duy nhất mà các Service trong Application layer nên dùng.
/// </summary>
public interface ICloudService
{
    /// <summary>
    /// Upload một file media lên đúng provider theo loại file.
    /// Ảnh → Cloudinary (auto optimize width 1920, quality 85).
    /// Video/Audio → R2.
    /// Validate magic bytes trước khi upload.
    /// </summary>
    /// <param name="file">IFormFile từ request.</param>
    /// <param name="folder">Folder đích (vd: "posts", "messages").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CloudUploadResult chứa SecureUrl, PublicId, StorageProvider...</returns>
    /// <exception cref="ArgumentNullException">File null.</exception>
    /// <exception cref="ArgumentException">File rỗng, quá lớn, sai định dạng hoặc bị giả mạo.</exception>
    Task<CloudUploadResult> UploadMediaAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload nhiều file song song (tối đa 10 file mỗi lần).
    /// Nếu bất kỳ file nào thất bại → tự động cleanup các file đã upload thành công.
    /// </summary>
    /// <param name="files">Danh sách file cần upload.</param>
    /// <param name="folder">Folder đích.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Danh sách CloudUploadResult theo thứ tự input.</returns>
    /// <exception cref="ArgumentException">Vượt quá 10 file.</exception>
    /// <exception cref="AggregateException">Một hoặc nhiều file upload thất bại (sau khi đã cleanup).</exception>
    Task<List<CloudUploadResult>> UploadMultipleAsync(
        IList<IFormFile> files,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa file khỏi đúng provider.
    /// Không throw nếu file không tồn tại (idempotent, best-effort).
    /// </summary>
    /// <param name="publicIdOrKey">PublicId (Cloudinary) hoặc object key (R2).</param>
    /// <param name="provider">Provider đang lưu file.</param>
    /// <param name="mediaType">Loại media — dùng để xác định ResourceType khi xóa Cloudinary.</param>
    Task DeleteMediaAsync(
        string publicIdOrKey,
        StorageProvider provider,
        MediaType mediaType);

    /// <summary>
    /// Lấy thống kê tổng hợp cả Cloudinary lẫn R2 cho admin dashboard.
    /// Chạy song song — nếu Cloudinary thất bại thì vẫn trả R2 stats (không fail toàn bộ).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AdminCloudStatsDto.</returns>
    Task<AdminCloudStatsDto> GetStatsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra file có phải ảnh hợp lệ theo ContentType không.</summary>
    bool IsImage(IFormFile file);

    /// <summary>Kiểm tra file có phải video hợp lệ theo ContentType không.</summary>
    bool IsVideo(IFormFile file);

    /// <summary>Kiểm tra file có phải audio hợp lệ theo ContentType không.</summary>
    bool IsAudio(IFormFile file);

    /// <summary>
    /// Validate magic bytes của file — chống giả mạo định dạng.
    /// Đọc 12 byte đầu, reset stream Position về 0 sau khi đọc.
    /// </summary>
    /// <param name="file">IFormFile cần kiểm tra.</param>
    /// <returns>True nếu magic bytes khớp ContentType, false nếu không.</returns>
    Task<bool> ValidateMagicBytesAsync(IFormFile file);
}