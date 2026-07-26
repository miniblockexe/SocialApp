namespace SocialApp.Domain.Entities;

/// <summary>
/// Hội thoại — có thể là chat 1-1 hoặc nhóm.
/// Không kế thừa BaseAuditableEntity — không cần soft-delete / UpdatedAt.
/// Tạo conversation 1-1 đã tồn tại → service trả conversation cũ (idempotent).
/// </summary>
public class Conversation
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>True = nhóm chat, False = chat 1-1.</summary>
    public bool IsGroup { get; set; } = false;

    /// <summary>Tên nhóm — null nếu là chat 1-1.</summary>
    public string? GroupName { get; set; }

    /// <summary>URL ảnh đại diện nhóm (Cloudinary) — null nếu là chat 1-1.</summary>
    public string? GroupAvatarUrl { get; set; }

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời điểm có tin nhắn mới nhất (UTC).
    /// Null khi hội thoại mới tạo, chưa có tin nhắn.
    /// Dùng để sort danh sách hội thoại theo tin nhắn gần nhất.
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    // Navigation properties

    /// <summary>Danh sách thành viên tham gia hội thoại.</summary>
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    /// <summary>Danh sách tin nhắn trong hội thoại.</summary>
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}