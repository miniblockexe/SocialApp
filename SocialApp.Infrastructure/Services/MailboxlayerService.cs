using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Xác thực email qua Mailboxlayer API.
/// Tài liệu: https://mailboxlayer.com/documentation
/// Free plan: 100 request/tháng.
/// </summary>
public sealed class MailboxlayerService : IEmailVerificationService
{
    private readonly HttpClient _http;
    private readonly MailboxlayerSettings _settings;
    private readonly ILogger<MailboxlayerService> _logger;

    public MailboxlayerService(
        HttpClient http,
        IOptions<MailboxlayerSettings> options,
        ILogger<MailboxlayerService> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<EmailVerificationResult?> VerifyAsync(string email, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return null; // skip trong dev

        try
        {
            // HTTP (không phải HTTPS) vì free plan không hỗ trợ HTTPS
            var url = $"http://apilayer.net/api/check?access_key={_settings.AccessKey}" +
                      $"&email={Uri.EscapeDataString(email)}&smtp=1&format=1";

            using var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Mailboxlayer] HTTP {Status} — skip verification", response.StatusCode);
                return null; // fail-open
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // API trả {"error": {...}} khi key sai hoặc hết quota
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("[Mailboxlayer] API error response: {Json}", json);
                return null;
            }

            var formatValid = root.TryGetProperty("format_valid", out var fv) && fv.GetBoolean();
            var smtpValid   = root.TryGetProperty("smtp_check",   out var sv) && sv.GetBoolean();
            var disposable  = root.TryGetProperty("disposable",   out var dv) && dv.GetBoolean();

            return new EmailVerificationResult(formatValid, smtpValid, disposable);
        }
        catch (Exception ex)
        {
            // Timeout, network, v.v — không chặn đăng ký
            _logger.LogError(ex, "[Mailboxlayer] Exception khi verify email: {Email}", email);
            return null;
        }
    }
}
