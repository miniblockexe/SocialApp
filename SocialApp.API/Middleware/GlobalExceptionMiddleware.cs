using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.Services;

namespace SocialApp.API.Middleware;

/// <summary>
/// Middleware bắt toàn bộ unhandled exception.
/// Log đầy đủ server-side, trả message chung về client — không leak stack trace / schema.
/// Đặt đầu tiên trong pipeline (app.UseGlobalExceptionMiddleware()).
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, startTime);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        DateTime startTime)
    {
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var endpoint = $"{context.Request.Method} {context.Request.Path}";
        var userId = context.User.FindFirst("sub")?.Value ?? "anonymous";

        // Map exception → status code + client message
        var (statusCode, clientMessage, errors) = MapException(exception);

        // Log đầy đủ server-side
        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "[{StatusCode}] {Endpoint} | userId={UserId} | elapsed={Elapsed}ms | {ExceptionType}: {ExceptionMessage}",
                statusCode, endpoint, userId, elapsed,
                exception.GetType().Name, exception.Message);
        }
        else
        {
            // 4xx — không cần log stack trace, chỉ log warning
            _logger.LogWarning(
                "[{StatusCode}] {Endpoint} | userId={UserId} | elapsed={Elapsed}ms | {ExceptionType}: {ExceptionMessage}",
                statusCode, endpoint, userId, elapsed,
                exception.GetType().Name, exception.Message);
        }

        // Trả response
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response đã bắt đầu gửi, không thể ghi thêm. Endpoint={Endpoint}", endpoint);
            return;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        object response = errors.Count > 0
            ? ApiResponse<object>.UnprocessableEntity(errors, clientMessage)
            : statusCode switch
            {
                400 => ApiResponse<object>.BadRequest(clientMessage),
                401 => ApiResponse<object>.Unauthorized(clientMessage),
                403 => ApiResponse<object>.Forbidden(clientMessage),
                404 => ApiResponse<object>.NotFound(clientMessage),
                409 => ApiResponse<object>.Conflict(clientMessage),
                413 => ApiResponse<object>.PayloadTooLarge(clientMessage),
                422 => ApiResponse<object>.UnprocessableEntity([], clientMessage),
                429 => ApiResponse<object>.TooManyRequests(clientMessage),
                _ => ApiResponse<object>.InternalServerError()
            };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await context.Response.WriteAsync(json);
    }

    // Exception → (statusCode, clientMessage, errors)

    private static (int StatusCode, string ClientMessage, List<string> Errors) MapException(Exception ex)
    {
        return ex switch
        {
            // 422 Unprocessable Entity
            // FluentValidation errors — phải đứng trước ArgumentException
            ValidationException ve => (
                StatusCodes.Status422UnprocessableEntity,
                "Dữ liệu không hợp lệ.",
                ve.Errors.Select(e => e.ErrorMessage).ToList()),

            // 400 Bad Request
            // Input không hợp lệ (argument, format)
            ArgumentException
            or ArgumentNullException
            or FormatException => (
                StatusCodes.Status400BadRequest,
                ex.Message,
                new List<string>()),

            // 401 Unauthorized
            // Chưa xác thực hoặc token không hợp lệ
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                ex.Message,
                new List<string>()),

            // 403 Forbidden
            // Đã xác thực nhưng không có quyền
            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                ex.Message,
                new List<string>()),

            // 404 Not Found
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                ex.Message,
                new List<string>()),

            // 409 Conflict
            // EF Core optimistic concurrency
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.",
                new List<string>()),

            // PostgreSQL unique constraint violation
            DbUpdateException { InnerException: PostgresException pg }
                when pg.SqlState == PostgresErrorCodes.UniqueViolation => (
                StatusCodes.Status409Conflict,
                "Dữ liệu đã tồn tại.",
                new List<string>()),

            // 400 Bad Request (DB)
            // PostgreSQL foreign key violation
            DbUpdateException { InnerException: PostgresException pgFk }
                when pgFk.SqlState == PostgresErrorCodes.ForeignKeyViolation => (
                StatusCodes.Status400BadRequest,
                "Dữ liệu liên quan không tồn tại.",
                new List<string>()),

            // 413 Payload Too Large
            BadHttpRequestException { StatusCode: 413 } => (
                StatusCodes.Status413PayloadTooLarge,
                "File vượt quá kích thước cho phép.",
                new List<string>()),

            // 429 Too Many Requests
            TooManyRequestsException tmr => (
                StatusCodes.Status429TooManyRequests,
                tmr.Message,
                new List<string>()),

            // 499 Client Closed Request
            // Client ngắt kết nối — không cần báo lỗi, chỉ log
            OperationCanceledException => (
                499,
                "Yêu cầu đã bị huỷ.",
                new List<string>()),

            // 400 Bad Request (Business Logic)
            // BusinessException: vi phạm quy tắc nghiệp vụ rõ ràng
            BusinessException => (
                StatusCodes.Status400BadRequest,
                ex.Message,
                new List<string>()),

            // 400 Bad Request (Fallback)
            // InvalidOperationException: business rule violations từ services
            // Đặt SAU tất cả exception cụ thể để tránh bắt nhầm lỗi system.
            // Chỉ rơi vào đây nếu không khớp với bất kỳ case nào ở trên.
            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                ex.Message,
                new List<string>()),

            // 500 Internal Server Error
            // Mọi exception còn lại — không leak thông tin chi tiết
            _ => (
                StatusCodes.Status500InternalServerError,
                "Đã xảy ra lỗi, vui lòng thử lại sau.",
                new List<string>())
        };
    }
}

// Extension method để đăng ký gọn
public static class GlobalExceptionMiddlewareExtensions
{
    // WebApplication overload — dùng trong Program.cs (minimal API)
    public static WebApplication UseGlobalExceptionHandler(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }

    // IApplicationBuilder overload — dùng trong Startup.cs hoặc test
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}