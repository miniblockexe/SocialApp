namespace SocialApp.Application.Settings;

public sealed class GmailSettings
{
    /// <summary>Địa chỉ Gmail dùng để gửi — ví dụ: myapp@gmail.com</summary>
    public string SenderEmail { get; init; } = string.Empty;

    /// <summary>Tên hiển thị trong trường From.</summary>
    public string SenderName { get; init; } = "SocialApp";

    /// <summary>
    /// App Password của Gmail (không phải mật khẩu đăng nhập).
    /// </summary>
    public string AppPassword { get; init; } = string.Empty;
}