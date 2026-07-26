using SocialApp.Application.DTOs.AI;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Interface cho Gemini AI service — xử lý chat với AI và health check.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Gửi tin nhắn tới Gemini AI, lưu cả user message lẫn AI response vào DB,
    /// đồng thời push AI message về client qua SignalR.
    /// </summary>
    /// <param name="userId">Id của user đang chat.</param>
    /// <param name="request">Request chứa conversationId, history và newMessage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>GeminiChatResponseDto chứa content, messageIds và tokensUsed.</returns>
    /// <exception cref="ArgumentException">userId/conversationId rỗng hoặc newMessage trống sau trim.</exception>
    /// <exception cref="UnauthorizedAccessException">User không có quyền với conversation.</exception>
    /// <exception cref="InvalidOperationException">Gemini API lỗi (quota, key không hợp lệ, service down...).</exception>
    Task<GeminiChatResponseDto> ChatAsync(
        Guid userId,
        GeminiChatRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check đơn giản — gửi ping tới Gemini API để kiểm tra service còn hoạt động không.
    /// Không throw exception — lỗi trả false và log warning.
    /// </summary>
    /// <returns>true nếu Gemini API đang hoạt động, false nếu không.</returns>
    Task<bool> IsServiceAvailableAsync();
}