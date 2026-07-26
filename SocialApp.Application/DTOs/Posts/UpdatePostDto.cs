using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// DTO cập nhật bài đăng.
/// Cả 2 field đều nullable — chỉ update field nào client gửi lên (có giá trị).
/// Không cho phép thêm/xóa media qua endpoint này (media quản lý riêng, đơn giản hóa).
/// </summary>
public sealed class UpdatePostDto
{
    /// <summary>Nội dung mới — null thì giữ nguyên nội dung cũ.</summary>
    public string? Content { get; init; }

    /// <summary>Quyền hiển thị mới — null thì giữ nguyên.</summary>
    public PostPrivacy? Privacy { get; init; }
}