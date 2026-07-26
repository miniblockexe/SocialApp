namespace SocialApp.Application.DTOs.AI;

/// <summary>
/// Một message trong lịch sử hội thoại với Gemini.
/// Gemini yêu cầu role phải là "user" hoặc "model", và history phải bắt đầu bằng "user".
/// </summary>
public sealed class GeminiMessageDto
{
    /// <summary>
    /// Vai trò của message. Giá trị hợp lệ: "user" | "model".
    /// "user" = tin nhắn người dùng, "model" = phản hồi của Gemini AI.
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Nội dung của message — không được để trống.</summary>
    public string Content { get; init; } = string.Empty;
}