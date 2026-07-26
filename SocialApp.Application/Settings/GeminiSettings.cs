namespace SocialApp.Application.Settings;

/// <summary>
/// Strongly-typed config cho Gemini AI — bind từ appsettings.json section "GeminiSettings".
/// Hỗ trợ tự động fallback sang FallbackModel khi Model chính hết quota (429).
/// </summary>
public sealed class GeminiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Danh sách model thử theo thứ tự ưu tiên từ trên xuống.
    /// Khi model trên hết quota (429/503/timeout) → tự động thử model tiếp theo.
    /// </summary>
    public List<string> Models { get; init; } =
    [
        "gemini-3.1-flash-lite",  // RPD 500 — free tier nhiều nhất
        "gemini-2.5-flash-lite",  // fallback
        "gemini-2.5-flash",       // fallback
        "gemini-3-flash-preview", // fallback
        "gemini-3.5-flash"        // fallback cuối
    ];

    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/";
    public int MaxOutputTokens { get; init; } = 1000;
    public double Temperature { get; init; } = 0.7;

    /// <summary>Giới hạn số message history gửi lên Gemini để tránh token quá lớn.</summary>
    public int MaxHistoryMessages { get; init; } = 20;

    /// <summary>Timeout HTTP request tới Gemini tính bằng giây.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>System prompt cấu hình personality của AI bot.</summary>
    public string SystemPrompt { get; init; } =
        "Bạn là trợ lý AI thân thiện trong ứng dụng mạng xã hội SocialApp. " +
        "Hãy trả lời ngắn gọn, hữu ích và phù hợp với ngữ cảnh trò chuyện.";
}