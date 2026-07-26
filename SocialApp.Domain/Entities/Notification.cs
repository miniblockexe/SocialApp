using SocialApp.Domain.Enums;

namespace SocialApp.Domain.Entities;

/// <summary>
/// Thông báo gửi đến user.
/// Không kế thừa BaseAuditableEntity — không cần soft-delete / UpdatedAt.
/// Self-notification (tự like bài mình) → service layer skip, không tạo notification.
/// </summary>
public class Notification
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → User nhận thông báo.</summary>
    public Guid UserId { get; set; }

    /// <summary>FK → User thực hiện hành động (người like, comment, gửi friend request...).</summary>
    public Guid ActorId { get; set; }

    /// <summary>
    /// Loại thông báo:
    /// Like=0 | Comment=1 | FriendRequest=2 | FriendAccepted=3 | Message=4 | System=5.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// ID của entity liên quan (PostId khi like/comment, MessageId khi nhắn tin...).
    /// Null cho thông báo hệ thống không liên quan entity cụ thể.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>Nội dung thông báo hiển thị — tối đa 500 ký tự.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True = đã đọc, False = chưa đọc.</summary>
    public bool IsRead { get; set; } = false;

    /// <summary>Thời điểm tạo thông báo (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>User nhận thông báo.</summary>
    public User User { get; set; } = null!;

    /// <summary>User thực hiện hành động tạo ra thông báo.</summary>
    public User Actor { get; set; } = null!;
}