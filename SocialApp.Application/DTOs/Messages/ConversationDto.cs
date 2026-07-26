using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.DTOs.Messages;

/// <summary>
/// DTO đại diện cho một conversation trả về client.
/// Dùng cho GET /api/conversations và sau khi tạo conversation mới.
/// UnreadCount và LastMessage được tính/set thủ công trong service.
/// </summary>
public sealed class ConversationDto
{
    /// <summary>Id của conversation.</summary>
    public Guid Id { get; init; }

    /// <summary>True = group conversation, false = 1-1.</summary>
    public bool IsGroup { get; init; }

    /// <summary>
    /// Tên group — null nếu IsGroup = false.
    /// Với 1-1: client tự hiển thị tên người kia.
    /// </summary>
    public string? GroupName { get; init; }

    /// <summary>
    /// Avatar của group — null nếu IsGroup = false hoặc chưa set.
    /// </summary>
    public string? GroupAvatarUrl { get; init; }

    /// <summary>
    /// Thời điểm tin nhắn cuối cùng (UTC) — null nếu chưa có message nào.
    /// Dùng để sort danh sách conversation.
    /// </summary>
    public DateTime? LastMessageAt { get; init; }

    /// <summary>
    /// Tin nhắn cuối cùng trong conversation — null nếu chưa có message.
    /// </summary>
    public MessageDto? LastMessage { get; init; }

    /// <summary>
    /// Số tin nhắn chưa đọc của user hiện tại trong conversation này.
    /// Tính từ ConversationParticipant.LastReadAt của user.
    /// </summary>
    public int UnreadCount { get; init; }

    /// <summary>
    /// Danh sách participant trong conversation (bao gồm cả user hiện tại).
    /// Với 1-1: 2 người. Với group: tất cả thành viên.
    /// </summary>
    public List<UserBriefDto> Participants { get; init; } = [];
}