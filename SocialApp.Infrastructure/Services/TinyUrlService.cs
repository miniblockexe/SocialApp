using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Rút gọn URL qua TinyURL free API (không cần key cho plan cơ bản).
/// Tài liệu: https://tinyurl.com/app/dev
/// </summary>
public sealed class TinyUrlService : IUrlShortenerService
{
    private readonly HttpClient _http;
    private readonly TinyUrlSettings _settings;
    private readonly ILogger<TinyUrlService> _logger;

    public TinyUrlService(
        HttpClient http,
        IOptions<TinyUrlSettings> options,
        ILogger<TinyUrlService> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<string> ShortenAsync(string longUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
            return longUrl;

        try
        {
            // Free tier: api-create.php (không cần key, trả text/plain)
            var url = $"https://tinyurl.com/api-create.php?url={Uri.EscapeDataString(longUrl)}";
            var shortened = await _http.GetStringAsync(url, ct);

            shortened = shortened.Trim();

            return shortened.StartsWith("https://tinyurl.com", StringComparison.OrdinalIgnoreCase)
                ? shortened
                : longUrl; // fallback: trả URL gốc nếu response bất thường
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TinyURL] Shorten thất bại cho URL: {Url}", longUrl);
            return longUrl;
        }
    }
}
