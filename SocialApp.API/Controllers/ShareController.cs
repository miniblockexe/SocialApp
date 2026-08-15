using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;
using System.Net;

namespace SocialApp.API.Controllers;

[ApiController]
[Route("share")]
[AllowAnonymous]
public sealed class ShareController : ControllerBase
{
    private readonly IPostRepository _postRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ShareController> _logger;

    public ShareController(
        IPostRepository postRepo,
        IConfiguration configuration,
        ILogger<ShareController> logger)
    {
        _postRepo = postRepo;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Trả về HTML nhỏ chứa OG meta tags + redirect 0 giây sang Angular frontend.
    /// Bài private / friends / bị xóa → redirect thẳng (không lộ nội dung qua OG).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> ShareRedirect(Guid id, CancellationToken ct)
    {
        var frontendBase = (_configuration["FrontendBaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
        var destinationUrl = $"{frontendBase}/posts/{id}";

        try
        {
            var post = await _postRepo.FirstOrDefaultAsync(
                p => p.Id == id && p.DeletedAt == null,
                ct,
                p => p.User,
                p => p.PostMediaFiles);

            if (post is null || post.Privacy != PostPrivacy.Public)
                return Redirect(destinationUrl);

            var title = $"{H(post.User.FullName)} – SocialApp";
            var snippet = post.Content?.Length > 0
                ? H(post.Content[..Math.Min(200, post.Content.Length)])
                : $"Xem bài viết của {H(post.User.FullName)} trên SocialApp";

            var imageUrl = post.PostMediaFiles
                .Where(m => m.MediaType == MediaType.Image)
                .Select(m => m.MediaUrl)
                .FirstOrDefault()
                ?? post.PostMediaFiles
                .Select(m => m.MediaUrl)
                .FirstOrDefault()
                ?? string.Empty;

            var hasImage = imageUrl.Length > 0;
            var twitterCard = hasImage ? "summary_large_image" : "summary";

            var ogImageTags = hasImage
                ? $"""
                      <meta property="og:image"        content="{H(imageUrl)}">
                      <meta property="og:image:width"  content="1200">
                      <meta property="og:image:height" content="630">
                      <meta name="twitter:image"       content="{H(imageUrl)}">
                  """
                : string.Empty;

            var html = $"""
                <!DOCTYPE html>
                <html lang="vi">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">

                  <meta http-equiv="refresh" content="0;url={destinationUrl}">

                  <title>{title}</title>
                  <meta name="description" content="{snippet}">

                  <meta property="og:type"        content="article">
                  <meta property="og:site_name"   content="SocialApp">
                  <meta property="og:title"       content="{title}">
                  <meta property="og:description" content="{snippet}">
                  <meta property="og:url"         content="{destinationUrl}">
                  {ogImageTags}

                  <!-- Twitter Card -->
                  <meta name="twitter:card"        content="{twitterCard}">
                  <meta name="twitter:title"       content="{title}">
                  <meta name="twitter:description" content="{snippet}">
                </head>
                <body style="margin:0;background:#0f1117;color:#fff;font-family:sans-serif;
                             display:flex;align-items:center;justify-content:center;height:100vh">
                  <p style="opacity:.6;font-size:14px">Đang chuyển hướng…</p>
                  <script>window.location.replace("{destinationUrl}");</script>
                </body>
                </html>
                """;

            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ShareRedirect lỗi. PostId={PostId}", id);
            return Redirect(destinationUrl);
        }
    }

    /// <summary>HTML-encode an toàn để chèn vào attribute và text node.</summary>
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
}