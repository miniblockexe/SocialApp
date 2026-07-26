using Microsoft.AspNetCore.Http;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// DTO tạo bài đăng mới.
/// Phải có Content HOẶC MediaFiles (không được cả 2 đều null/empty) — validate ở CreatePostValidator.
/// </summary>
public sealed class CreatePostDto
{
    /// <summary>Nội dung bài đăng — nullable nếu có media (ảnh/video) đi kèm.</summary>
    public string? Content { get; init; }

    /// <summary>Quyền hiển thị — mặc định Public.</summary>
    public PostPrivacy Privacy { get; init; } = PostPrivacy.Public;

    /// <summary>
    /// File đính kèm (ảnh/video) — nullable, tối đa 10 file, mỗi file tối đa 200MB
    /// (giới hạn validate ở CreatePostValidator).
    /// </summary>
    public IList<IFormFile>? MediaFiles { get; init; }
}