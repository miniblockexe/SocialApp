namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Request body để admin cấm tài khoản user.
/// Reason bắt buộc — validate bởi BanUserValidator (10–500 ký tự, không toàn whitespace).
/// </summary>
public sealed class BanUserDto
{
    /// <summary>Lý do cấm tài khoản — bắt buộc, 10–500 ký tự.</summary>
    public string Reason { get; init; } = string.Empty;
}