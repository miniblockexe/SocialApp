using System.Net;
using System.Text.Json;
using SocialApp.Application.Common;
using SocialApp.Application.Interfaces.Services;
using SocialApp.API.Extensions;
using SocialApp.Infrastructure.Services;
using SocialApp.Application.Interfaces.Repositories;

namespace SocialApp.API.Middleware;

/// <summary>
/// Middleware kiểm tra user bị ban sau khi đã xác thực JWT.
/// Phải đặt SAU UseAuthentication() và UseAuthorization() trong pipeline.
/// User bị ban nhưng token còn hạn → 403 Forbidden (không revoke token, chỉ chặn request).
/// Anonymous request → bỏ qua, không check.
/// </summary>
public class BannedUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BannedUserMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BannedUserMiddleware(
        RequestDelegate next,
        ILogger<BannedUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Chỉ check nếu user đã authenticate
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        var userId = context.User.GetUserId();

        // Guid.Empty → token hỏng hoặc không có sub claim → bỏ qua, để Auth middleware xử lý
        if (userId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        // IBanStatusChecker là Scoped → lấy từ request scope
        var banChecker = context.RequestServices.GetRequiredService<IBanStatusChecker>();

        var isBanned = await banChecker.IsUserBannedAsync(userId, context.RequestAborted);

        if (isBanned)
        {
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            _logger.LogWarning(
                "[403] Banned user attempted access | userId={UserId} | endpoint={Endpoint}",
                userId, endpoint);

            if (context.Response.HasStarted)
                return;

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            var response = ApiResponse<object>.Forbidden(
                "Tài khoản của bạn đã bị khoá. Vui lòng liên hệ hỗ trợ.");

            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }
}

// Extension method để đăng ký gọn
public static class BannedUserMiddlewareExtensions
{
    public static IApplicationBuilder UseBannedUserCheck(this IApplicationBuilder app)
        => app.UseMiddleware<BannedUserMiddleware>();
}