using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Notifications;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Interface cho toàn bộ business logic liên quan đến thông báo.
/// CreateNotificationAsync là non-critical — không throw nếu lỗi SignalR.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Tạo notification và push realtime qua SignalR.
    /// Self-notification (recipientId == actorId) → silently skip, không throw.
    /// Duplicate trong 5 phút (cùng recipient + actor + type + entityId) → silently skip.
    /// Lỗi SignalR → log warning, không throw (notification đã lưu DB là đủ).
    /// </summary>
    Task CreateNotificationAsync(
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        Guid? entityId,
        string content);

    /// <summary>
    /// Lấy danh sách thông báo của user, kèm thông tin actor.
    /// OrderBy CreatedAt DESC, phân trang.
    /// </summary>
    Task<PagedResult<NotificationDto>> GetNotificationsAsync(
        Guid userId, int page, int size);

    /// <summary>
    /// Lấy số lượng thông báo chưa đọc và tổng số thông báo của user.
    /// </summary>
    Task<NotificationCountDto> GetUnreadCountAsync(Guid userId);

    /// <summary>
    /// Đánh dấu nhiều notification là đã đọc.
    /// notificationIds rỗng → no-op.
    /// Notification không thuộc về userId → silent ignore (không throw 403).
    /// Push UpdateNotificationCount mới qua SignalR sau khi update.
    /// </summary>
    Task MarkAsReadAsync(Guid userId, List<Guid> notificationIds);

    /// <summary>
    /// Đánh dấu toàn bộ notification chưa đọc của user là đã đọc (1 query).
    /// Push UpdateNotificationCount = 0 qua SignalR.
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId);

    /// <summary>
    /// Xóa một notification. Chỉ owner mới được xóa.
    /// </summary>
    Task DeleteNotificationAsync(Guid userId, Guid notificationId);
}