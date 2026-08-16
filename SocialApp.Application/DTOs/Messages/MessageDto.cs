using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.DTOs.Messages;

/// <summary>
/// DTO đại diện cho một tin nhắn trả về client.
/// Dùng cho GET messages, SignalR ReceiveMessage, DeleteMessage.
/// Nếu IsDeleted = true: Content = null, AttachmentUrl = null, SharedPost = null.
/// </summary>
public sealed class MessageDto
{
    /// <summary>Id của message.</summary>
    public Guid Id { get; init; }

    /// <summary>Id của conversation chứa message này.</summary>
    public Guid ConversationId { get; init; }

    /// <summary>
    /// Nội dung tin nhắn — null nếu IsDeleted = true hoặc tin nhắn chỉ có file/shared post.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>True nếu tin nhắn do Gemini AI tạo ra.</summary>
    public bool IsAI { get; init; }

    /// <summary>
    /// URL file đính kèm — null nếu không có file hoặc IsDeleted = true.
    /// </summary>
    public string? AttachmentUrl { get; init; }

    /// <summary>
    /// Loại file đính kèm: "image" | "video" | "audio" | "gif" | null.
    /// </summary>
    public string? AttachmentType { get; init; }

    /// <summary>
    /// Preview bài viết được chia sẻ — null nếu không phải tin nhắn share.
    /// Khi IsDeleted = true: null.
    /// </summary>
    public SharedPostPreviewDto? SharedPost { get; init; }

    /// <summary>Thời điểm tạo tin nhắn (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>True nếu tin nhắn đã bị soft-delete.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Thông tin người gửi.</summary>
    public UserBriefDto Sender { get; init; } = null!;

    /// <summary>
    /// Danh sách userId đã seen tin nhắn này.
    /// Map từ MessageSeen.Select(s => s.UserId).
    /// </summary>
    public List<Guid> SeenByUserIds { get; init; } = [];
}