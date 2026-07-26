namespace SocialApp.Application.DTOs.AI;

/// <summary>
/// Request body để gửi tin nhắn tới Gemini AI trong một conversation.
/// Client gửi kèm history để Gemini có ngữ cảnh trò chuyện.
/// </summary>
public sealed class GeminiChatRequestDto
{
    /// <summary>
    /// Id của conversation chứa cuộc trò chuyện AI.
    /// Dùng để verify quyền truy cập và lưu message vào đúng conversation.
    /// </summary>
    public Guid ConversationId { get; init; }

    /// <summary>
    /// Lịch sử trò chuyện trước đó — client gửi lên để Gemini có context.
    /// Service sẽ giới hạn tối đa MaxHistoryMessages từ cuối list.
    /// Null hoặc rỗng = bắt đầu cuộc trò chuyện mới.
    /// </summary>
    public List<GeminiMessageDto> History { get; init; } = [];

    /// <summary>
    /// Tin nhắn mới của user gửi tới AI.
    /// Bắt buộc, tối đa 2000 ký tự, không được toàn whitespace.
    /// </summary>
    public string NewMessage { get; init; } = string.Empty;
}