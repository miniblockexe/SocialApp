using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Thông tin bài đăng dành cho admin — bao gồm cả bài đã xóa và lý do xóa.
/// </summary>
public sealed class AdminPostDto
{
    public Guid Id { get; init; }

    /// <summary>Nội dung bài đăng — null nếu post chỉ có media.</summary>
    public string? Content { get; init; }

    public PostPrivacy Privacy { get; init; }

    // Trạng thái xóa

    public bool IsDeleted { get; init; }

    /// <summary>Thời điểm bị xóa (UTC) — null nếu chưa xóa.</summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>True nếu bài đăng bị xóa bởi admin (không phải chính chủ).</summary>
    public bool DeletedByAdmin { get; init; }

    /// <summary>Lý do admin xóa — null nếu chưa bị admin xóa.</summary>
    public string? AdminDeleteReason { get; init; }

    // Timestamps

    /// <summary>Thời điểm tạo bài đăng (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Thời điểm cập nhật gần nhất (UTC).</summary>
    public DateTime UpdatedAt { get; init; }

    // Tác giả

    /// <summary>Thông tin tóm tắt tác giả.</summary>
    public UserBriefDto Author { get; init; } = null!;

    // Thống kê

    /// <summary>Số file media đính kèm.</summary>
    public int MediaCount { get; init; }

    /// <summary>Số lượt thích.</summary>
    public int LikeCount { get; init; }

    /// <summary>Số bình luận chưa bị xóa.</summary>
    public int CommentCount { get; init; }
}