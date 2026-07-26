using SocialApp.Application.DTOs.Messages;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Abstraction cho SignalR ChatHub push operations.
/// Implement bởi API layer (ChatHubService) để tránh circular dependency:
/// Application → API.
/// GeminiService inject interface này thay vì IHubContext&lt;ChatHub&gt; trực tiếp.
/// </summary>
public interface IChatHub
{
    /// <summary>
    /// Push tin nhắn mới đến tất cả client trong conversation group.
    /// Event: ReceiveMessage.
    /// </summary>
    Task SendMessageAsync(Guid conversationId, MessageDto message);
}