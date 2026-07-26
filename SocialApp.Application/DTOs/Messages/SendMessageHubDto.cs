namespace SocialApp.Application.DTOs.Messages;

/// <summary>
/// DTO gửi tin nhắn qua SignalR ChatHub.
/// Không có IFormFile — file upload phải đi qua HTTP endpoint.
/// Dùng riêng cho hub method SendMessage.
/// </summary>
public sealed class SendMessageHubDto
{
    /// <summary>Id của conversation cần gửi tin nhắn vào.</summary>
    public Guid ConversationId { get; init; }

    /// <summary>
    /// Nội dung tin nhắn — không được null/whitespace khi gửi qua hub
    /// (vì không có file đính kèm).
    /// Tối đa 4000 ký tự.
    /// </summary>
    public string? Content { get; init; }
}