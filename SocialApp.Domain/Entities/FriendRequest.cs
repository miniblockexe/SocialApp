using SocialApp.Domain.Enums;

namespace SocialApp.Domain.Entities;

/// <summary>
/// Lời mời kết bạn / quan hệ bạn bè giữa 2 user.
/// Không kế thừa BaseAuditableEntity — không cần soft-delete.
/// Edge cases xử lý ở service layer:
///   - B gửi request cho A khi A đã gửi cho B → auto accept.
///   - Block người đang là bạn → hủy kết bạn trước rồi block.
/// Unique constraint (SenderId, ReceiverId) cấu hình trong AppDbContext.
/// </summary>
public class FriendRequest
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → User gửi lời mời.</summary>
    public Guid SenderId { get; set; }

    /// <summary>FK → User nhận lời mời.</summary>
    public Guid ReceiverId { get; set; }

    /// <summary>
    /// Trạng thái quan hệ:
    /// Pending = 0 | Accepted = 1 | Rejected = 2 | Blocked = 3.
    /// </summary>
    public FriendStatus Status { get; set; } = FriendStatus.Pending;

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm cập nhật trạng thái gần nhất (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>User gửi lời mời.</summary>
    public User Sender { get; set; } = null!;

    /// <summary>User nhận lời mời.</summary>
    public User Receiver { get; set; } = null!;
}