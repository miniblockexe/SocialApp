using SocialApp.Application.DTOs.Notifications;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Abstraction cho SignalR NotificationHub push operations.
/// Implement bởi API layer (NotificationHubService) để tránh circular dependency:
/// Application → API.
/// NotificationService inject interface này thay vì IHubContext&lt;NotificationHub&gt; trực tiếp.
/// </summary>
public interface INotificationHub
{
    /// <summary>
    /// Push thông báo mới đến một user cụ thể qua SignalR group "user_{userId}".
    /// Event: ReceiveNotification.
    /// </summary>
    Task SendNotificationAsync(Guid userId, NotificationDto notification);

    /// <summary>
    /// Push số lượng thông báo chưa đọc mới đến user qua SignalR group "user_{userId}".
    /// Event: UpdateNotificationCount.
    /// </summary>
    Task SendNotificationCountAsync(Guid userId, NotificationCountDto count);
}