using AutoMapper;
using Microsoft.Extensions.Logging;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Notifications;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Services;

/// <summary>
/// Implement INotificationService: tạo, đọc, đánh dấu đã đọc, xóa thông báo.
/// CreateNotificationAsync là non-critical: lỗi SignalR log warning, không throw.
/// Dùng INotificationHub (abstraction) thay vì IHubContext trực tiếp để tránh
/// circular dependency Application → API.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IUserRepository _userRepo;
    private readonly INotificationHub _notificationHub;
    private readonly IMapper _mapper;
    private readonly ILogger<NotificationService> _logger;

    // Khoảng thời gian kiểm tra duplicate notification (tránh spam like/unlike)
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(5);

    public NotificationService(
        INotificationRepository notificationRepo,
        IUserRepository userRepo,
        INotificationHub notificationHub,
        IMapper mapper,
        ILogger<NotificationService> logger)
    {
        _notificationRepo = notificationRepo;
        _userRepo = userRepo;
        _notificationHub = notificationHub;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task CreateNotificationAsync(
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        Guid? entityId,
        string content)
    {
        // Self-notification → skip, không throw
        if (recipientId == actorId) return;

        // Reject Guid.Empty
        if (recipientId == Guid.Empty || actorId == Guid.Empty)
        {
            _logger.LogWarning(
                "CreateNotificationAsync: recipientId hoặc actorId là Guid.Empty. Bỏ qua.");
            return;
        }

        // Kiểm tra recipient tồn tại — notification là non-critical, không throw
        var recipient = await _userRepo.GetByIdAsync(recipientId);
        if (recipient is null)
        {
            _logger.LogWarning(
                "CreateNotificationAsync: Recipient {RecipientId} không tồn tại. Bỏ qua.",
                recipientId);
            return;
        }

        // Kiểm tra duplicate trong 5 phút
        var since = DateTime.UtcNow.Subtract(DuplicateWindow);
        var isDuplicate = await _notificationRepo.ExistsDuplicateAsync(
            recipientId, actorId, (int)type, entityId, since);

        if (isDuplicate)
        {
            _logger.LogDebug(
                "CreateNotificationAsync: Duplicate notification skipped. " +
                "Recipient={RecipientId}, Actor={ActorId}, Type={Type}, EntityId={EntityId}",
                recipientId, actorId, type, entityId);
            return;
        }

        // Trim content, giới hạn 500 ký tự theo entity constraint
        var safeContent = string.IsNullOrWhiteSpace(content)
            ? string.Empty
            : content.Trim()[..Math.Min(content.Trim().Length, 500)];

        // Tạo và lưu notification
        var notification = new Notification
        {
            UserId = recipientId,
            ActorId = actorId,
            Type = type,
            EntityId = entityId,
            Content = safeContent,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _notificationRepo.AddAsync(notification);
        await _notificationRepo.SaveChangesAsync();

        _logger.LogInformation(
            "Notification created: Id={Id}, Recipient={RecipientId}, Actor={ActorId}, Type={Type}",
            notification.Id, recipientId, actorId, type);

        // Push realtime qua SignalR — non-critical, INotificationHub đã wrap try-catch bên trong
        try
        {
            var actor = await _userRepo.GetByIdAsync(actorId);
            if (actor is null) return;

            notification.Actor = actor;
            var notificationDto = MapToDto(notification);

            await _notificationHub.SendNotificationAsync(recipientId, notificationDto);

            var unreadCount = await _notificationRepo.CountUnreadAsync(recipientId);
            var totalCount = await _notificationRepo.CountTotalAsync(recipientId);
            var countDto = new NotificationCountDto
            {
                UnreadCount = unreadCount,
                TotalCount = totalCount
            };

            await _notificationHub.SendNotificationCountAsync(recipientId, countDto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Push realtime thất bại cho notification {NotificationId}, recipient {RecipientId}. " +
                "Notification đã được lưu DB.",
                notification.Id, recipientId);
        }
    }

    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(
        Guid userId, int page, int size)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = size < 1 ? 10 : size > 100 ? 100 : size;
        var skip = (safePage - 1) * safeSize;

        var (items, totalCount) = await _notificationRepo.GetPagedAsync(userId, skip, safeSize);

        var dtos = items.Select(MapToDto).ToList();

        return PagedResult<NotificationDto>.Create(dtos, totalCount, safePage, safeSize);
    }

    public async Task<NotificationCountDto> GetUnreadCountAsync(Guid userId)
    {
        var unreadCount = await _notificationRepo.CountUnreadAsync(userId);
        var totalCount = await _notificationRepo.CountTotalAsync(userId);

        return new NotificationCountDto
        {
            UnreadCount = unreadCount,
            TotalCount = totalCount
        };
    }

    public async Task MarkAsReadAsync(Guid userId, List<Guid> notificationIds)
    {
        if (notificationIds.Count == 0) return;

        // Chỉ lấy notification thuộc về userId và chưa đọc — silent ignore phần còn lại
        var notifications = await _notificationRepo.GetByIdsAndUserAsync(userId, notificationIds);

        if (notifications.Count == 0) return;

        foreach (var n in notifications)
            n.IsRead = true;

        await _notificationRepo.SaveChangesAsync();

        _logger.LogInformation(
            "MarkAsRead: UserId={UserId}, Updated={Count} notifications",
            userId, notifications.Count);

        // Push UpdateNotificationCount mới — non-critical
        try
        {
            var unreadCount = await _notificationRepo.CountUnreadAsync(userId);
            var totalCount = await _notificationRepo.CountTotalAsync(userId);
            await _notificationHub.SendNotificationCountAsync(userId, new NotificationCountDto
            {
                UnreadCount = unreadCount,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SignalR push thất bại sau MarkAsRead. UserId={UserId}", userId);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var affected = await _notificationRepo.MarkAllAsReadAsync(userId);

        _logger.LogInformation(
            "MarkAllAsRead: UserId={UserId}, Updated={Count} notifications",
            userId, affected);

        // Push UpdateNotificationCount = 0 — non-critical
        try
        {
            var totalCount = await _notificationRepo.CountTotalAsync(userId);
            await _notificationHub.SendNotificationCountAsync(userId, new NotificationCountDto
            {
                UnreadCount = 0,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SignalR push thất bại sau MarkAllAsRead. UserId={UserId}", userId);
        }
    }

    public async Task DeleteNotificationAsync(Guid userId, Guid notificationId)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException("NotificationId không hợp lệ.");

        var notification = await _notificationRepo.GetByIdAsync(notificationId);

        if (notification is null)
            throw new KeyNotFoundException("Thông báo không tồn tại.");

        if (notification.UserId != userId)
            throw new ForbiddenException("Bạn không có quyền xóa thông báo này.");

        _notificationRepo.Remove(notification);
        await _notificationRepo.SaveChangesAsync();

        _logger.LogInformation(
            "Notification deleted: Id={Id}, UserId={UserId}",
            notificationId, userId);
    }

    private static string ResolveEntityType(NotificationType type) => type switch
    {
        NotificationType.Like or NotificationType.Comment => "post",
        NotificationType.FriendRequest or NotificationType.FriendAccepted => "friend_request",
        NotificationType.Message => "message",
        _ => "system"
    };

    private NotificationDto MapToDto(Notification n)
    {
        return new NotificationDto
        {
            Id = n.Id,
            Type = n.Type,
            Content = n.Content,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            Actor = _mapper.Map<UserBriefDto>(n.Actor),
            EntityId = n.EntityId,
            EntityType = ResolveEntityType(n.Type)
        };
    }
}