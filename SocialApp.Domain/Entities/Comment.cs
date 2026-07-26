using SocialApp.Domain.Common;

namespace SocialApp.Domain.Entities;

/// <summary>
/// Bình luận trên bài đăng.
/// Hỗ trợ self-reference 1 cấp (comment → reply).
/// Reply vào reply (nested > 1 cấp) bị chặn ở service layer → 400.
/// Kế thừa BaseAuditableEntity: Id, CreatedAt, UpdatedAt, DeletedAt, IsDeleted (soft-delete).
/// </summary>
public class Comment : BaseAuditableEntity
{
    /// <summary>FK → Post chứa comment này.</summary>
    public Guid PostId { get; set; }

    /// <summary>FK → User tác giả comment.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// FK → Comment cha (self-reference).
    /// Null = comment gốc. Có giá trị = reply.
    /// Service layer từ chối reply vào reply (ParentComment.ParentCommentId != null).
    /// </summary>
    public Guid? ParentCommentId { get; set; }

    /// <summary>Nội dung bình luận — tối đa 2000 ký tự.</summary>
    public string Content { get; set; } = string.Empty;

    // Navigation properties

    /// <summary>Bài đăng chứa comment này.</summary>
    public Post Post { get; set; } = null!;

    /// <summary>Tác giả comment.</summary>
    public User User { get; set; } = null!;

    /// <summary>Comment cha — null nếu là comment gốc.</summary>
    public Comment? ParentComment { get; set; }

    /// <summary>Danh sách reply trực tiếp (chỉ 1 cấp).</summary>
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}