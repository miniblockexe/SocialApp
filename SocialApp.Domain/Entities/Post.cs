using System.Xml.Linq;
using SocialApp.Domain.Common;
using SocialApp.Domain.Enums;

namespace SocialApp.Domain.Entities;

/// <summary>
/// Bài đăng của người dùng.
/// Kế thừa BaseAuditableEntity: Id, CreatedAt, UpdatedAt, DeletedAt, IsDeleted (soft-delete).
/// IsDeleted / DeletedAt từ base đã đủ — không cần thêm field riêng.
/// </summary>
public class Post : BaseAuditableEntity
{
    /// <summary>FK → User tác giả.</summary>
    public Guid UserId { get; set; }

    /// <summary>Nội dung bài đăng — tối đa 5000 ký tự, nullable (post chỉ có media).</summary>
    public string? Content { get; set; }

    /// <summary>Quyền hiển thị: Public = 0, Friends = 1, OnlyMe = 2.</summary>
    public PostPrivacy Privacy { get; set; } = PostPrivacy.Public;

    /// <summary>
    /// hiển thị placeholder "Bài viết gốc đã bị xóa").
    /// </summary>
    public Guid? OriginalPostId { get; set; }

    // Navigation properties

    /// <summary>Tác giả bài đăng.</summary>
    public User User { get; set; } = null!;

    /// <summary>Danh sách file media đính kèm (ảnh / video / audio).</summary>
    public ICollection<PostMediaFile> PostMediaFiles { get; set; } = new List<PostMediaFile>();

    /// <summary>Danh sách lượt thích.</summary>
    public ICollection<Like> Likes { get; set; } = new List<Like>();

    /// <summary>Danh sách bình luận.</summary>
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    /// <summary>Bài gốc (self-ref, nullable). Null nếu đây là bài gốc.</summary>
    public Post? OriginalPost { get; set; }

    /// <summary>Các bài đã chia sẻ lại bài này (inverse của OriginalPost).</summary>
    public ICollection<Post> Shares { get; set; } = new List<Post>();

    /// <summary>FK → Group nếu bài thuộc nhóm, null nếu bài cá nhân.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Navigation → Group (null nếu bài cá nhân).</summary>
    public Group? Group { get; set; }

    /// <summary>Metadata duyệt bài trong nhóm (null nếu bài cá nhân).</summary>
    public GroupPost? GroupPost { get; set; }
}