namespace SocialApp.Domain.Entities;

/// <summary>
/// Tin nhắn trong hội thoại.
/// Không kế thừa BaseAuditableEntity — soft-delete qua IsDeleted riêng,
/// không cần UpdatedAt (tin nhắn không được chỉnh sửa).
/// </summary>
public class Message
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary> Conversation chứa tin nhắn này.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>User gửi tin nhắn.</summary>
    public Guid SenderId { get; set; }

    /// <summary>
    /// Nội dung tin nhắn — tối đa 4000 ký tự.
    /// Nullable: tin nhắn chỉ có attachment hoặc shared post không cần content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// True = tin nhắn do Gemini AI tạo ra (MediaBot / assistant).
    /// False = tin nhắn của người dùng thật.
    /// </summary>
    public bool IsAI { get; set; } = false;

    /// <summary>URL file đính kèm (ảnh / video / audio) — nullable.</summary>
    public string? AttachmentUrl { get; set; }

    /// <summary>
    /// Loại file đính kèm (MIME type hoặc enum string) — nullable.
    /// Ví dụ: "image/jpeg", "video/mp4", "audio/mpeg".
    /// </summary>
    public string? AttachmentType { get; set; }

    /// <summary>
    /// Post được chia sẻ vào cuộc trò chuyện — nullable.
    /// Khi có: client render card preview bài viết trong bubble tin nhắn.
    /// </summary>
    public Guid? SharedPostId { get; set; }

    /// <summary>
    /// Soft-delete: True = tin nhắn đã bị xoá (hiển thị "Tin nhắn đã được thu hồi").
    /// Không xoá vật lý để giữ ngữ cảnh hội thoại.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>Thời điểm gửi (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Hội thoại chứa tin nhắn này.</summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>User gửi tin nhắn.</summary>
    public User Sender { get; set; } = null!;

    /// <summary>Bài viết được chia sẻ — null nếu không phải tin nhắn share.</summary>
    public Post? SharedPost { get; set; }

    /// <summary>Danh sách user đã đọc tin nhắn này.</summary>
    public ICollection<MessageSeen> SeenBy { get; set; } = new List<MessageSeen>();
}