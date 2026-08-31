using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Gửi email qua Gmail SMTP dùng MailKit.
/// Cần bật "App Password" trong tài khoản Google (không dùng mật khẩu thường).
/// </summary>
public sealed class GmailEmailService : IEmailService
{
    private readonly GmailSettings _settings;
    private readonly ILogger<GmailEmailService> _logger;

    public GmailEmailService(
        IOptions<GmailSettings> settings,
        ILogger<GmailEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Đặt lại mật khẩu SocialApp";

        message.Body = new TextPart("html")
        {
            Text = BuildResetEmailHtml(toName, resetToken)
        };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation(
                "[Email] Gửi reset password thành công → {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Email] Gửi reset password thất bại → {Email}", toEmail);
            // Không throw — caller không cần biết lỗi SMTP để tránh user enumeration
        }
    }

    private static string BuildResetEmailHtml(string name, string token) => $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#0f0f10;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="520" cellpadding="0" cellspacing="0"
                     style="background:#1a1a1e;border-radius:16px;overflow:hidden;border:1px solid #2a2a30;">
                <!-- Header -->
                <tr>
                  <td style="background:linear-gradient(135deg,#e74c3c,#c0392b);padding:32px 40px;text-align:center;">
                    <div style="font-size:28px;font-weight:800;color:#fff;letter-spacing:-0.5px;">S SocialApp</div>
                  </td>
                </tr>
                <!-- Body -->
                <tr>
                  <td style="padding:40px;color:#e0e0e8;">
                    <h2 style="margin:0 0 16px;font-size:22px;color:#fff;">Đặt lại mật khẩu</h2>
                    <p style="margin:0 0 12px;color:#a0a0b0;">Xin chào <strong style="color:#fff">{name}</strong>,</p>
                    <p style="margin:0 0 28px;color:#a0a0b0;line-height:1.6;">
                      Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.
                      Sử dụng mã OTP bên dưới — mã có hiệu lực trong <strong style="color:#fff">15 phút</strong>.
                    </p>
                    <!-- OTP box -->
                    <div style="background:#0f0f10;border:1px solid #3a3a44;border-radius:12px;
                                padding:28px;text-align:center;margin-bottom:28px;">
                      <div style="font-size:42px;font-weight:800;letter-spacing:12px;
                                  color:#e74c3c;font-variant-numeric:tabular-nums;">{token}</div>
                      <div style="margin-top:8px;color:#606070;font-size:13px;">Mã OTP xác thực</div>
                    </div>
                    <p style="margin:0;color:#606070;font-size:13px;line-height:1.6;">
                      Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.
                      Tài khoản của bạn vẫn an toàn.
                    </p>
                  </td>
                </tr>
                <!-- Footer -->
                <tr>
                  <td style="padding:20px 40px;border-top:1px solid #2a2a30;
                             text-align:center;color:#606070;font-size:12px;">
                    © 2025 SocialApp · Email này được gửi tự động, vui lòng không trả lời.
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}