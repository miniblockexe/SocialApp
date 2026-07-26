using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SocialApp.API.Extensions;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.API.Hubs;

/// <summary>
/// SignalR hub xử lý thông báo real-time.
///
/// Mỗi user join group "user_{userId}" khi connect để nhận notification riêng tư.
/// Push ngay unread count hiện tại về caller khi connect.
///
/// Events client nhận  : ReceiveNotification | UpdateNotificationCount
/// Events client gọi   : MarkRead
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        INotificationService notificationService,
        ILogger<NotificationHub> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    // Lifecycle

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = Context.User?.GetUserId() ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                _logger.LogWarning(
                    "NotificationHub: kết nối bị từ chối — userId không hợp lệ. ConnectionId={ConnectionId}",
                    Context.ConnectionId);
                Context.Abort();
                return;
            }

            // Kiểm tra ban status — user bị ban không được kết nối hub
            var banChecker = Context.GetHttpContext()!
                .RequestServices.GetRequiredService<IBanStatusChecker>();
            if (await banChecker.IsUserBannedAsync(userId))
            {
                _logger.LogWarning(
                    "NotificationHub: banned user bị từ chối kết nối — UserId={UserId}, ConnectionId={ConnectionId}",
                    userId, Context.ConnectionId);
                Context.Abort();
                return;
            }

            // Join group cá nhân — dùng để push notification riêng tư
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.User(userId));

            // Push ngay unread count hiện tại về caller
            var count = await _notificationService.GetUnreadCountAsync(userId);
            await Clients.Caller.SendAsync("UpdateNotificationCount", count);

            _logger.LogInformation(
                "NotificationHub: User={UserId} connected, unreadCount={Count}. ConnectionId={ConnectionId}",
                userId, count.UnreadCount, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotificationHub.OnConnectedAsync thất bại. ConnectionId={ConnectionId}",
                Context.ConnectionId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = Context.User?.GetUserId() ?? Guid.Empty;

            if (exception is not null)
                _logger.LogWarning(
                    "NotificationHub: User={UserId} disconnected with error: {Error}. ConnectionId={ConnectionId}",
                    userId, exception.Message, Context.ConnectionId);
            else
                _logger.LogInformation(
                    "NotificationHub: User={UserId} disconnected. ConnectionId={ConnectionId}",
                    userId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotificationHub.OnDisconnectedAsync thất bại. ConnectionId={ConnectionId}",
                Context.ConnectionId);
        }
    }

    // Hub methods

    /// <summary>
    /// Đánh dấu đã đọc các notification theo Id.
    /// Push UpdateNotificationCount mới về caller sau khi mark.
    /// Non-critical — chỉ log nếu lỗi, không gửi error về client.
    /// </summary>
    public async Task MarkRead(List<Guid> notificationIds)
    {
        var userId = Guid.Empty;
        try
        {
            userId = Context.User?.GetUserId() ?? Guid.Empty;
            if (userId == Guid.Empty) return;

            if (notificationIds is null || notificationIds.Count == 0) return;

            // Lọc bỏ Guid.Empty khỏi list trước khi xử lý
            var validIds = notificationIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (validIds.Count == 0) return;

            await _notificationService.MarkAsReadAsync(userId, validIds);

            // Push count mới về caller
            var count = await _notificationService.GetUnreadCountAsync(userId);
            await Clients.Caller.SendAsync("UpdateNotificationCount", count);

            _logger.LogInformation(
                "NotificationHub.MarkRead: UserId={UserId}, Marked={Count} notifications",
                userId, validIds.Count);
        }
        catch (Exception ex)
        {
            // Non-critical — chỉ log, không push error về client
            _logger.LogWarning(ex,
                "NotificationHub.MarkRead thất bại. UserId={UserId}",
                userId);
        }
    }
}