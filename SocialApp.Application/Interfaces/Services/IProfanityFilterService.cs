namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Lọc từ ngữ tục tĩu (PurgoMalum API — free, không cần key).
/// Fail-open: trả false (sạch) nếu API không phản hồi — không chặn user vô cớ.
/// </summary>
public interface IProfanityFilterService
{
    /// <summary>
    /// Kiểm tra text có chứa profanity không.
    /// Returns true = có từ tục → từ chối; false = sạch → cho qua.
    /// </summary>
    Task<bool> ContainsProfanityAsync(string text, CancellationToken ct = default);
}
