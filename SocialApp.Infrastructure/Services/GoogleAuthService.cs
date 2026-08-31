using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Verify Google ID Token bằng Google tokeninfo endpoint.
/// Không cần Google SDK — chỉ một HTTP GET.
/// </summary>
public sealed class GoogleAuthService : IGoogleAuthService
{
    private readonly HttpClient _http;
    private readonly GoogleSettings _settings;
    private readonly ILogger<GoogleAuthService> _logger;

    // Endpoint Google cung cấp để verify token phía server
    private const string TokenInfoUrl =
        "https://oauth2.googleapis.com/tokeninfo?id_token={0}";

    public GoogleAuthService(
        HttpClient http,
        IOptions<GoogleSettings> settings,
        ILogger<GoogleAuthService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken)
    {
        try
        {
            var url = string.Format(TokenInfoUrl, Uri.EscapeDataString(idToken));
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[GoogleAuth] Token verify thất bại — status: {Status}",
                    response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Kiểm tra audience — phải khớp ClientId trong config
            var aud = root.TryGetProperty("aud", out var audProp)
                ? audProp.GetString() : null;

            if (aud != _settings.ClientId)
            {
                _logger.LogWarning(
                    "[GoogleAuth] audience không khớp — aud: {Aud}, expected: {Expected}",
                    aud, _settings.ClientId);
                return null;
            }

            // Kiểm tra token chưa hết hạn
            if (root.TryGetProperty("exp", out var expProp)
                && long.TryParse(expProp.GetString(), out var exp))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
                if (expiry < DateTime.UtcNow)
                {
                    _logger.LogWarning("[GoogleAuth] Token đã hết hạn.");
                    return null;
                }
            }

            var emailVerified = root.TryGetProperty("email_verified", out var evProp)
                && evProp.GetString() == "true";

            return new GoogleUserInfo
            {
                GoogleId = root.TryGetProperty("sub", out var sub) ? sub.GetString()! : string.Empty,
                Email = root.TryGetProperty("email", out var em) ? em.GetString()! : string.Empty,
                Name = root.TryGetProperty("name", out var nm) ? nm.GetString()! : string.Empty,
                Picture = root.TryGetProperty("picture", out var pic) ? pic.GetString() : null,
                EmailVerified = emailVerified
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GoogleAuth] Lỗi khi verify token.");
            return null;
        }
    }
}