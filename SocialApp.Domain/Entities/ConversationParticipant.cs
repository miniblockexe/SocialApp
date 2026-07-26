namespace SocialApp.Domain.Entities;

/// <summary>
/// Bảng trung gian — thành viên tham gia hội thoại.
/// PK composite (ConversationId, UserId) cấu hình trong AppDbContext.
/// Không kế thừa BaseAuditableEntity — không cần Id riêng / soft-delete.
/// </summary>
public class ConversationParticipant
{
    /// <summary>FK → Conversation (phần 1 của composite PK).</summary>
    public Guid ConversationId { get; set; }

    /// <summary>FK → User (phần 2 của composite PK).</summary>
    public Guid UserId { get; set; }

    /// <summary>Thời điểm tham gia hội thoại (UTC).</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời điểm đọc tin nhắn gần nhất (UTC).
    /// Null = chưa đọc tin nào kể từ khi tham gia.
    /// Dùng để tính số tin nhắn chưa đọc.
    /// </summary>
    public DateTime? LastReadAt { get; set; }

    // Navigation properties

    /// <summary>Hội thoại mà thành viên này thuộc về.</summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>Thành viên tham gia.</summary>
    public User User { get; set; } = null!;
}