using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialApp.API.Extensions;
using SocialApp.Application.DTOs.Messages;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.API.Hubs;

/// <summary>
/// SignalR hub xử lý real-time messaging.
///
/// Group naming (dùng HubGroups helper):
///   user_{userId}   — group cá nhân, nhận notification riêng tư
///   conv_{convId}   — group conversation, nhận broadcast tin nhắn
///
/// Events client nhận  : ReceiveMessage | MessageSeen | UserTyping | MessageDeleted | Error
/// Events client gọi   : SendMessage | MarkSeen | SendTyping | DeleteMessage
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IMessageDbContext _db;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageService messageService,
        IMessageDbContext db,
        IUserRepository userRepo,
        ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _db = db;
        _userRepo = userRepo;
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
                    "ChatHub: kết nối bị từ chối — userId không hợp lệ. ConnectionId={ConnectionId}",
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
                    "ChatHub: banned user bị từ chối kết nối — UserId={UserId}, ConnectionId={ConnectionId}",
                    userId, Context.ConnectionId);
                Context.Abort();
                return;
            }

            // Join group cá nhân
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.User(userId));

            // Join tất cả conversation groups của user
            var convIds = await _db.ConversationParticipants
                .Where(p => p.UserId == userId)
                .Select(p => p.ConversationId)
                .ToListAsync();

            foreach (var convId in convIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Conversation(convId));

            // Cập nhật LastSeen
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is not null)
            {
                user.LastSeen = DateTime.UtcNow;
                _userRepo.Update(user);
                await _userRepo.SaveChangesAsync();
            }

            _logger.LogInformation(
                "ChatHub: User={UserId} connected, joined {Count} conversation groups. ConnectionId={ConnectionId}",
                userId, convIds.Count, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatHub.OnConnectedAsync thất bại. ConnectionId={ConnectionId}",
                Context.ConnectionId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = Context.User?.GetUserId() ?? Guid.Empty;

            if (userId != Guid.Empty)
            {
                var user = await _userRepo.GetByIdAsync(userId);
                if (user is not null)
                {
                    user.LastSeen = DateTime.UtcNow;
                    _userRepo.Update(user);
                    await _userRepo.SaveChangesAsync();
                }
            }

            if (exception is not null)
                _logger.LogWarning(
                    "ChatHub: User={UserId} disconnected with error: {Error}. ConnectionId={ConnectionId}",
                    userId, exception.Message, Context.ConnectionId);
            else
                _logger.LogInformation(
                    "ChatHub: User={UserId} disconnected. ConnectionId={ConnectionId}",
                    userId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatHub.OnDisconnectedAsync thất bại. ConnectionId={ConnectionId}",
                Context.ConnectionId);
        }
    }

    // Hub methods

    /// <summary>
    /// Gửi tin nhắn text qua SignalR (file dùng HTTP POST /api/conversations/{id}/messages).
    /// Broadcast MessageDto tới toàn bộ conversation group.
    /// </summary>
    public async Task SendMessage(SendMessageHubDto dto)
    {
        var userId = Guid.Empty;
        try
        {
            userId = Context.User?.GetUserId() ?? Guid.Empty;
            if (userId == Guid.Empty) return;

            if (dto.ConversationId == Guid.Empty)
            {
                await Clients.Caller.SendAsync("Error", new
                {
                    method = "SendMessage",
                    message = "ConversationId không hợp lệ."
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(dto.Content?.Trim()))
            {
                await Clients.Caller.SendAsync("Error", new
                {
                    method = "SendMessage",
                    message = "Nội dung tin nhắn không được để trống."
                });
                return;
            }

            var messageDto = await _messageService.SendMessageFromHubAsync(userId, dto);

            await Clients
                .Group(HubGroups.Conversation(dto.ConversationId))
                .SendAsync("ReceiveMessage", messageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatHub.SendMessage thất bại. UserId={UserId}, ConvId={ConvId}",
                userId, dto.ConversationId);

            await Clients.Caller.SendAsync("Error", new
            {
                method = "SendMessage",
                message = "Không thể gửi tin nhắn."
            });
        }
    }

    /// <summary>
    /// Đánh dấu đã đọc toàn bộ tin nhắn trong conversation.
    /// Broadcast MessageSeen event tới group — non-critical, không gửi error nếu lỗi.
    /// </summary>
    public async Task MarkSeen(Guid conversationId)
    {
        var userId = Guid.Empty;
        try
        {
            userId = Context.User?.GetUserId() ?? Guid.Empty;
            if (userId == Guid.Empty || conversationId == Guid.Empty) return;

            await _messageService.MarkAsSeenAsync(userId, conversationId);

            await Clients
                .Group(HubGroups.Conversation(conversationId))
                .SendAsync("MessageSeen", new
                {
                    conversationId,
                    userId,
                    seenAt = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            // Non-critical — chỉ log, không push error về client
            _logger.LogWarning(ex,
                "ChatHub.MarkSeen thất bại. UserId={UserId}, ConvId={ConvId}",
                userId, conversationId);
        }
    }

    /// <summary>
    /// Broadcast trạng thái đang gõ tới các thành viên khác trong conversation.
    /// Không lưu DB — fire and forget.
    /// </summary>
    public async Task SendTyping(Guid conversationId, bool isTyping)
    {
        var userId = Guid.Empty;
        try
        {
            userId = Context.User?.GetUserId() ?? Guid.Empty;
            if (userId == Guid.Empty || conversationId == Guid.Empty) return;

            // OthersInGroup — không gửi lại cho chính caller
            await Clients
                .OthersInGroup(HubGroups.Conversation(conversationId))
                .SendAsync("UserTyping", new
                {
                    conversationId,
                    userId,
                    isTyping
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ChatHub.SendTyping thất bại. UserId={UserId}, ConvId={ConvId}",
                userId, conversationId);
        }
    }

    /// <summary>
    /// Xóa tin nhắn (soft delete) và broadcast MessageDeleted tới conversation group.
    /// </summary>
    public async Task DeleteMessage(Guid messageId)
    {
        var userId = Guid.Empty;
        try
        {
            userId = Context.User?.GetUserId() ?? Guid.Empty;
            if (userId == Guid.Empty || messageId == Guid.Empty) return;

            var result = await _messageService.DeleteMessageAsync(userId, messageId);

            await Clients
                .Group(HubGroups.Conversation(result.ConversationId))
                .SendAsync("MessageDeleted", new
                {
                    messageId,
                    conversationId = result.ConversationId,
                    deletedBy = userId
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatHub.DeleteMessage thất bại. UserId={UserId}, MessageId={MessageId}",
                userId, messageId);

            await Clients.Caller.SendAsync("Error", new
            {
                method = "DeleteMessage",
                message = "Không thể xóa tin nhắn."
            });
        }
    }
    /// <summary>
    /// Thông báo cuộc gọi đến cho peer qua SignalR.
    /// </summary>
    public async Task CallInvite(Guid conversationId, string mode)
    {
        var callerId = Context.User?.GetUserId() ?? Guid.Empty;
        if (callerId == Guid.Empty) return;

        var caller = await _userRepo.GetByIdAsync(callerId);
        if (caller is null) return;

        await Clients
            .OthersInGroup(HubGroups.Conversation(conversationId))
            .SendAsync("IncomingCall", new
            {
                conversationId,
                callerId,
                callerName = caller.FullName,
                callerAvatar = caller.AvatarUrl,
                mode // "audio" | "video"
            });
    }

    /// <summary>
    /// Báo cho caller biết callee từ chối.
    /// </summary>
    public async Task CallDeclined(Guid conversationId)
    {
        var userId = Context.User?.GetUserId() ?? Guid.Empty;
        if (userId == Guid.Empty) return;

        await Clients
            .OthersInGroup(HubGroups.Conversation(conversationId))
            .SendAsync("CallDeclined", new { conversationId, userId });
    }
}