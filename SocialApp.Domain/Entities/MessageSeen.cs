namespace SocialApp.Domain.Entities;

/// <summary>
/// Bảng trung gian — tracking user đã đọc tin nhắn.
/// PK composite (MessageId, UserId) cấu hình trong AppDbContext.
/// Không kế thừa BaseAuditableEntity — không cần Id riêng / soft-delete.
/// </summary>
public class MessageSeen
{
    /// <summary>FK → Message đã được đọc (phần 1 của composite PK).</summary>
    public Guid MessageId { get; set; }

    /// <summary>FK → User đã đọc tin nhắn (phần 2 của composite PK).</summary>
    public Guid UserId { get; set; }

    /// <summary>Thời điểm đọc tin nhắn (UTC).</summary>
    public DateTime SeenAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>Tin nhắn được đọc.</summary>
    public Message Message { get; set; } = null!;

    /// <summary>User đã đọc tin nhắn.</summary>
    public User User { get; set; } = null!;
}