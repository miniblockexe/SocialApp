using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SocialApp.API.Hubs;
using SocialApp.Application.DTOs.Notifications;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.API.Services;

/// <summary>
/// Implement INotificationHub bằng SignalR IHubContext&lt;NotificationHub&gt;.
/// Đăng ký trong API layer để tránh circular dependency Application → API.
/// </summary>
public sealed class NotificationHubService : INotificationHub
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationHubService> _logger;

    public NotificationHubService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, NotificationDto notification)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notification);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SignalR SendNotification thất bại. UserId={UserId}, NotificationId={NotificationId}",
                userId, notification.Id);
        }
    }

    public async Task SendNotificationCountAsync(Guid userId, NotificationCountDto count)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("UpdateNotificationCount", count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SignalR SendNotificationCount thất bại. UserId={UserId}",
                userId);
        }
    }
}