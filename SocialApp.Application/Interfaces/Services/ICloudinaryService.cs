using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using SocialApp.Application.DTOs.Cloud;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Contract cho Cloudinary service — upload ảnh/video và xóa file.
/// Interface ở Application layer, implementation ở Infrastructure.
/// Application layer không phụ thuộc trực tiếp vào CloudinaryDotNet SDK
/// ngoại trừ enum ResourceType dùng để xóa đúng loại file.
/// </summary>
public interface ICloudinaryService
{
    /// <summary>
    /// Upload ảnh lên Cloudinary với tùy chọn resize và nén.
    /// Validate ContentType, magic bytes và kích thước trước khi upload.
    /// </summary>
    /// <param name="file">IFormFile từ request.</param>
    /// <param name="folder">Folder đích trên Cloudinary (vd: "avatars", "posts").</param>
    /// <param name="maxWidthPx">Giới hạn chiều rộng tối đa (px). Null = không resize.</param>
    /// <param name="qualityPercent">Chất lượng nén (1-100). Null = auto quality.</param>
    /// <returns>CloudUploadResult chứa SecureUrl, PublicId, dimensions...</returns>
    /// <exception cref="ArgumentException">File null, rỗng, sai định dạng hoặc quá lớn.</exception>
    /// <exception cref="InvalidOperationException">Upload thất bại — lỗi từ Cloudinary.</exception>
    Task<CloudUploadResult> UploadImageAsync(
        IFormFile file,
        string folder,
        int? maxWidthPx = null,
        int? qualityPercent = null);

    /// <summary>
    /// Upload video lên Cloudinary.
    /// Validate ContentType, magic bytes và kích thước trước khi upload.
    /// </summary>
    /// <param name="file">IFormFile từ request.</param>
    /// <param name="folder">Folder đích trên Cloudinary.</param>
    /// <returns>CloudUploadResult chứa SecureUrl, PublicId...</returns>
    /// <exception cref="ArgumentException">File null, rỗng, sai định dạng hoặc quá lớn.</exception>
    /// <exception cref="InvalidOperationException">Upload thất bại — lỗi từ Cloudinary.</exception>
    Task<CloudUploadResult> UploadVideoAsync(
        IFormFile file,
        string folder);

    /// <summary>
    /// Xóa file trên Cloudinary theo publicId.
    /// Không throw nếu file không tồn tại (idempotent, best-effort).
    /// </summary>
    /// <param name="publicId">Public ID trên Cloudinary.</param>
    /// <param name="resourceType">Image hoặc Video (Cloudinary phân biệt 2 loại khi xóa).</param>
    Task DeleteAsync(string publicId, ResourceType resourceType);

    /// <summary>
    /// Lấy dung lượng đã dùng trên Cloudinary (MB).
    /// Gọi Cloudinary Usage API với Basic Auth.
    /// Trả về 0 nếu gọi API thất bại (không throw).
    /// </summary>
    Task<double> GetUsageMBAsync();
}