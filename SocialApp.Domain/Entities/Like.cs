namespace SocialApp.Domain.Entities;

/// <summary>
/// Lượt thích của user cho một bài đăng.
/// Không kế thừa BaseAuditableEntity — không cần soft-delete / UpdatedAt.
/// Toggle logic (like → unlike) xử lý ở service layer, không throw lỗi.
/// Unique constraint (UserId, PostId) được cấu hình trong AppDbContext.
/// </summary>
public class Like
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → User đã like.</summary>
    public Guid UserId { get; set; }

    /// <summary>FK → Post được like.</summary>
    public Guid PostId { get; set; }

    /// <summary>Thời điểm like (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>User đã like.</summary>
    public User User { get; set; } = null!;

    /// <summary>Bài đăng được like.</summary>
    public Post Post { get; set; } = null!;
}