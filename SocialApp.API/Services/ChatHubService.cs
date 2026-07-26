using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SocialApp.API.Hubs;
using SocialApp.Application.DTOs.Messages;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.API.Services;

/// <summary>
/// Implement IChatHub bằng SignalR IHubContext&lt;ChatHub&gt;.
/// Đăng ký trong API layer để tránh circular dependency Application → API.
/// Dùng bởi GeminiService để push AI message về conversation group.
/// </summary>
public sealed class ChatHubService : IChatHub
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatHubService> _logger;

    public ChatHubService(
        IHubContext<ChatHub> hubContext,
        ILogger<ChatHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendMessageAsync(Guid conversationId, MessageDto message)
    {
        try
        {
            await _hubContext.Clients
                .Group($"conv_{conversationId}")
                .SendAsync("ReceiveMessage", message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SignalR SendMessage thất bại. ConversationId={ConversationId}, MessageId={MessageId}",
                conversationId, message.Id);
        }
    }
}