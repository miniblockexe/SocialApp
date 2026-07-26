namespace SocialApp.Application.DTOs.Notifications;

/// <summary>
/// DTO trả về số lượng thông báo.
/// Dùng cho GET /api/notifications/count và SignalR push event UpdateNotificationCount.
/// </summary>
public sealed class NotificationCountDto
{
    /// <summary>Số thông báo chưa đọc.</summary>
    public int UnreadCount { get; init; }

    /// <summary>Tổng số thông báo (đã đọc + chưa đọc).</summary>
    public int TotalCount { get; init; }
}