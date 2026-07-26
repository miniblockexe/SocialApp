using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Notifications;

/// <summary>
/// DTO đại diện cho một thông báo trả về client.
/// Dùng cho GET /api/notifications và SignalR push event ReceiveNotification.
/// </summary>
public sealed class NotificationDto
{
    /// <summary>Id của notification record.</summary>
    public Guid Id { get; init; }

    /// <summary>Loại thông báo.</summary>
    public NotificationType Type { get; init; }

    /// <summary>Nội dung thông báo hiển thị — tối đa 500 ký tự.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>True = đã đọc, False = chưa đọc.</summary>
    public bool IsRead { get; init; }

    /// <summary>Thời điểm tạo thông báo (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>User thực hiện hành động tạo ra thông báo (người like, comment, gửi request...).</summary>
    public UserBriefDto Actor { get; init; } = null!;

    /// <summary>
    /// Id của entity liên quan:
    /// PostId khi like/comment, FriendRequestId khi friend request/accepted,
    /// MessageId khi nhắn tin. Null cho thông báo hệ thống.
    /// </summary>
    public Guid? EntityId { get; init; }

    /// <summary>
    /// Loại entity liên quan để client điều hướng:
    /// "post" | "friend_request" | "message" | "system".
    /// </summary>
    public string EntityType { get; init; } = string.Empty;
}