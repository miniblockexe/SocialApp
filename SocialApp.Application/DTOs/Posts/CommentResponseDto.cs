using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// DTO trả về khi hiển thị 1 bình luận (hoặc reply).
/// RepliesCount, IsOwner được tính thủ công trong service, không map qua AutoMapper.
/// </summary>
public sealed class CommentResponseDto
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Thời điểm cập nhật gần nhất (UTC).</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Tác giả bình luận.</summary>
    public UserBriefDto Author { get; init; } = null!;

    /// <summary>Số lượng reply trực tiếp (chỉ hỗ trợ 1 cấp).</summary>
    public int RepliesCount { get; set; }

    /// <summary>Null = comment gốc. Có giá trị = đây là reply vào comment gốc đó.</summary>
    public Guid? ParentCommentId { get; init; }

    /// <summary>Viewer hiện tại có phải tác giả comment này không.</summary>
    public bool IsOwner { get; set; }
}