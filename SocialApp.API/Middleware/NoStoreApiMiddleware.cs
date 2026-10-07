namespace SocialApp.API.Middleware;

/// <summary>
/// Thêm "Cache-Control: no-store" cho mọi response /api/* (trừ endpoint đã tự đặt Cache-Control,
/// ví dụ ShareController có [ResponseCache]) để proxy/CDN phía trước (Cloudflare Worker)
/// không cache dữ liệu theo từng user như profile, trạng thái kết bạn, trạng thái ban.
/// </summary>
public class NoStoreApiMiddleware
{
    private readonly RequestDelegate _next;

    public NoStoreApiMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                if (!headers.ContainsKey("Cache-Control"))
                {
                    headers["Cache-Control"] = "no-store";
                    headers["Pragma"] = "no-cache";
                }
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }
}

public static class NoStoreApiMiddlewareExtensions
{
    public static IApplicationBuilder UseNoStoreApi(this IApplicationBuilder app)
        => app.UseMiddleware<NoStoreApiMiddleware>();
}
