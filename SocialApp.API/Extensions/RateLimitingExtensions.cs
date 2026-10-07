using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SocialApp.API.Extensions;

/// <summary>
/// Rate limiting theo từng client (IP hoặc user), thay cho AddApplicationRateLimiting cũ.
/// Cũ: AddFixedWindowLimiter tạo MỘT bộ đếm dùng chung cho cả hệ thống (ví dụ "register" 3/giờ cho TẤT CẢ mọi người),
///     và UseRateLimiter đặt trước UseRouting nên các [EnableRateLimiting] không có hiệu lực.
/// Mới: mỗi client có bộ đếm riêng; 429 kèm header Retry-After và body JSON chuẩn.
/// Yêu cầu: app.UseRateLimiter() phải đặt SAU app.UseRouting() và SAU app.UseAuthentication().
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddClientRateLimiting(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var message = "Quá nhiều yêu cầu. Vui lòng thử lại sau.";
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    http.Response.Headers["Retry-After"] = seconds.ToString();
                    message = $"Quá nhiều yêu cầu. Vui lòng thử lại sau {seconds} giây.";
                }

                await http.Response.WriteAsJsonAsync(
                    new { success = false, message, errors = Array.Empty<string>() },
                    ct);
            };

            // Ẩn danh (đăng nhập/đăng ký/OTP): theo IP
            AddPolicy(options, config, "login", "Login", 5, 900, byUser: false);
            AddPolicy(options, config, "register", "Register", 3, 3600, byUser: false);

            // Đã đăng nhập: theo user (chưa đăng nhập thì theo IP)
            AddPolicy(options, config, "upload", "Upload", 20, 60, byUser: true);
            AddPolicy(options, config, "gemini", "GeminiAI", 10, 60, byUser: true);
            AddPolicy(options, config, "default", "Default", 100, 60, byUser: true);
        });

        return services;
    }

    private static void AddPolicy(
        RateLimiterOptions options,
        IConfiguration config,
        string policyName,
        string section,
        int defaultPermit,
        int defaultWindowSeconds,
        bool byUser)
    {
        var permit = config.GetValue($"RateLimitSettings:{section}:PermitLimit", defaultPermit);
        var window = TimeSpan.FromSeconds(
            config.GetValue($"RateLimitSettings:{section}:WindowSeconds", defaultWindowSeconds));

        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ResolveClientKey(httpContext, byUser),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit,
                    Window = window,
                    QueueLimit = 0
                }));
    }

    private static string ResolveClientKey(HttpContext httpContext, bool byUser)
    {
        if (byUser && httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.GetUserId();
            if (userId != Guid.Empty)
                return "u:" + userId;
        }

        // Cùng cách lấy IP với AuthController.GetClientIpAddress
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return "ip:" + forwarded.Split(',')[0].Trim();

        return "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
