using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.API.Extensions;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SocialApp.API.Controllers;

[ApiController]
[Route("api/turn")]
[Authorize]
public sealed class TurnController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TurnController> _logger;

    public TurnController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TurnController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Tạo short-lived TURN credential từ Cloudflare Calls API.
    /// Credential hết hạn sau 24h.
    /// </summary>
    [HttpGet("credentials")]
    public async Task<IActionResult> GetCredentials()
    {
        var tokenId = _configuration["CloudflareTurnSettings:TurnTokenId"];
        var apiToken = _configuration["CloudflareTurnSettings:ApiToken"];

        if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(apiToken))
        {
            _logger.LogWarning("Cloudflare TURN credentials not configured");
            return StatusCode(503, new { message = "TURN server not configured" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiToken);

            var url = $"https://rtc.live.cloudflare.com/v1/turn/keys/{tokenId}/credentials/generate";
            var body = JsonContent.Create(new { ttl = 86400 }); // 24h

            var response = await client.PostAsync(url, body);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            var iceServersEl = data.GetProperty("iceServers");
            var iceServersArray = iceServersEl.ValueKind == JsonValueKind.Array
                ? iceServersEl
                : JsonSerializer.Deserialize<JsonElement>($"[{iceServersEl.GetRawText()}]");

            return Ok(new { iceServers = iceServersArray });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch TURN credentials from Cloudflare");
            return StatusCode(502, new { message = "Failed to get TURN credentials" });
        }
    }
}