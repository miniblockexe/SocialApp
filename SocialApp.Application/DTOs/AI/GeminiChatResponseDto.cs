namespace SocialApp.Application.DTOs.AI;

/// <summary>
/// Response trả về sau khi Gemini xử lý tin nhắn.
/// Bao gồm nội dung AI, Id của 2 message đã lưu vào DB, và usage metadata.
/// </summary>
public sealed class GeminiChatResponseDto
{
    /// <summary>Nội dung phản hồi từ Gemini AI.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Id của conversation chứa cuộc trò chuyện này.</summary>
    public Guid ConversationId { get; init; }

    /// <summary>Id của Message user vừa gửi — đã được lưu vào DB.</summary>
    public Guid UserMessageId { get; init; }

    /// <summary>Id của Message AI response — đã được lưu vào DB (IsAI = true).</summary>
    public Guid AiMessageId { get; init; }

    /// <summary>
    /// Tổng số token đã dùng trong request này (cả input lẫn output).
    /// Null nếu Gemini không trả về usage metadata.
    /// </summary>
    public int? TokensUsed { get; init; }
}