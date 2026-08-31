namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Gửi email transactional (reset mật khẩu, v.v.)
/// Implementation dùng Gmail SMTP qua MailKit.
/// </summary>
public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken);
}