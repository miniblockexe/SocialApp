namespace SocialApp.API.Middleware;

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