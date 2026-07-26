namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Request body để admin xóa bài đăng.
/// Reason bắt buộc — validate bởi AdminDeletePostValidator (5–500 ký tự, không toàn whitespace).
/// </summary>
public sealed class AdminDeletePostDto
{
    /// <summary>Lý do xóa bài đăng — bắt buộc, 5–500 ký tự.</summary>
    public string Reason { get; init; } = string.Empty;
}