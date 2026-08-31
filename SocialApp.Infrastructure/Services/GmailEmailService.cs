using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;

namespace SocialApp.Infrastructure.Services;

public sealed class GmailEmailService : IEmailService
{
    private readonly GmailSettings _settings;
    private readonly HttpClient _http;
    private readonly ILogger<GmailEmailService> _logger;

    // Cache access token
    private string? _cachedAccessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public GmailEmailService(
        IOptions<GmailSettings> settings,
        HttpClient http,
        ILogger<GmailEmailService> logger)
    {
        _settings = settings.Value;
        _http = http;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetToken)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            var rawMessage = BuildRawMessage(toEmail, toName, resetToken);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            request.Content = new StringContent(
                JsonSerializer.Serialize(new { raw = rawMessage }),
                Encoding.UTF8,
                "application/json");

            var response = await _http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Email] Gửi thành công → {Email}", toEmail);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("[Email] Gmail API lỗi {Status} → {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] Gửi thất bại → {Email}", toEmail);
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        // Dùng cached token nếu còn hạn
        if (_cachedAccessToken is not null && DateTime.UtcNow < _tokenExpiry)
            return _cachedAccessToken;

        var response = await _http.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.OAuthClientId,
                ["client_secret"] = _settings.OAuthClientSecret,
                ["refresh_token"] = _settings.OAuthRefreshToken,
                ["grant_type"] = "refresh_token"
            }));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        _cachedAccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

        return _cachedAccessToken;
    }

    private string BuildRawMessage(string toEmail, string toName, string token)
    {
        var html = BuildHtml(toName, token);

        // RFC 2822 format
        var email = new StringBuilder();
        email.AppendLine($"From: {_settings.SenderName} <{_settings.SenderEmail}>");
        email.AppendLine($"To: {toName} <{toEmail}>");
        email.AppendLine("Subject: =?utf-8?B?" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes("Đặt lại mật khẩu SocialApp")) + "?=");
        email.AppendLine("MIME-Version: 1.0");
        email.AppendLine("Content-Type: text/html; charset=utf-8");
        email.AppendLine("Content-Transfer-Encoding: base64");
        email.AppendLine();
        email.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(html)));

        // Base64url encode (Gmail API yêu cầu)
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(email.ToString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string BuildHtml(string name, string token) => $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#0f0f10;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="520" cellpadding="0" cellspacing="0"
                     style="background:#1a1a1e;border-radius:16px;overflow:hidden;border:1px solid #2a2a30;">
                <tr>
                  <td style="background:linear-gradient(135deg,#e74c3c,#c0392b);padding:32px 40px;text-align:center;">
                    <div style="font-size:28px;font-weight:800;color:#fff;">SocialApp</div>
                  </td>
                </tr>
                <tr>
                  <td style="padding:40px;color:#e0e0e8;">
                    <h2 style="margin:0 0 16px;font-size:22px;color:#fff;">Đặt lại mật khẩu</h2>
                    <p style="margin:0 0 12px;color:#a0a0b0;">Xin chào <strong style="color:#fff">{name}</strong>,</p>
                    <p style="margin:0 0 28px;color:#a0a0b0;line-height:1.6;">
                      Mã OTP bên dưới có hiệu lực trong <strong style="color:#fff">15 phút</strong>.
                    </p>
                    <div style="background:#0f0f10;border:1px solid #3a3a44;border-radius:12px;
                                padding:28px;text-align:center;margin-bottom:28px;">
                      <div style="font-size:42px;font-weight:800;letter-spacing:12px;color:#e74c3c;">{token}</div>
                      <div style="margin-top:8px;color:#606070;font-size:13px;">Mã OTP xác thực</div>
                    </div>
                    <p style="margin:0;color:#606070;font-size:13px;">
                      Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:20px 40px;border-top:1px solid #2a2a30;text-align:center;color:#606070;font-size:12px;">
                    © 2026 SocialApp · Email tự động, vui lòng không trả lời.
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}