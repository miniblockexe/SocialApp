using Microsoft.AspNetCore.Http;
using SocialApp.Application.DTOs.Cloud;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Contract cho Cloudflare R2 service (S3-compatible) — upload, xóa, liệt kê file video/audio.
/// Interface ở Application layer, implementation ở Infrastructure.
/// </summary>
public interface IR2Service
{
    /// <summary>
    /// Upload file lên R2.
    /// Validate ContentType whitelist, magic bytes và kích thước trước khi upload.
    /// </summary>
    /// <param name="file">IFormFile từ request.</param>
    /// <param name="folder">Folder đích trên bucket (vd: "videos", "audio").</param>
    /// <param name="customFileName">Tên file tùy chỉnh. Null = tự sinh Guid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CloudUploadResult chứa SecureUrl, PublicId (object key)...</returns>
    /// <exception cref="ArgumentException">File null, rỗng, sai định dạng hoặc quá lớn.</exception>
    /// <exception cref="InvalidOperationException">Upload thất bại — lỗi từ R2/S3.</exception>
    /// <exception cref="OperationCanceledException">Request bị hủy.</exception>
    Task<CloudUploadResult> UploadAsync(
        IFormFile file,
        string folder,
        string? customFileName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa file trên R2 theo object key.
    /// Không throw nếu file không tồn tại (idempotent — R2 trả 204 kể cả key không tồn tại).
    /// </summary>
    /// <param name="key">S3 object key.</param>
    Task DeleteAsync(string key);

    /// <summary>
    /// Kiểm tra file có tồn tại trên R2 hay không.
    /// </summary>
    /// <param name="key">S3 object key.</param>
    /// <returns>True nếu tồn tại, false nếu không.</returns>
    /// <exception cref="InvalidOperationException">Lỗi khác 404 khi gọi S3.</exception>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Liệt kê file trên R2 theo prefix.
    /// Sắp xếp theo LastModified DESC.
    /// </summary>
    /// <param name="prefix">Prefix lọc (vd: "videos/"). Null = liệt kê tất cả.</param>
    /// <param name="maxKeys">Số file tối đa trả về (default 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Danh sách R2FileInfo.</returns>
    Task<IEnumerable<R2FileInfo>> ListFilesAsync(
        string? prefix = null,
        int maxKeys = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thống kê tổng quan storage R2.
    /// Scan tối đa 1000 file để tính toán.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>R2StorageStats chứa tổng file, dung lượng, phân loại theo type.</returns>
    Task<R2StorageStats> GetStorageStatsAsync(
        CancellationToken cancellationToken = default);
}