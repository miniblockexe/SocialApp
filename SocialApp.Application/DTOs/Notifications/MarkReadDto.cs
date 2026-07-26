namespace SocialApp.Application.DTOs.Notifications;

/// <summary>
/// DTO cho endpoint PUT /api/notifications/read.
/// Cho phép mark nhiều notification là đã đọc trong một request.
/// </summary>
public sealed class MarkReadDto
{
    /// <summary>
    /// Danh sách Id của các notification cần đánh dấu đã đọc.
    /// Cho phép rỗng — service sẽ no-op nếu list rỗng.
    /// Notification không thuộc về user hiện tại sẽ bị silent ignore (không throw 403).
    /// </summary>
    public List<Guid> NotificationIds { get; init; } = [];
}