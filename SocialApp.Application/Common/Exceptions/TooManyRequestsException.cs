namespace SocialApp.Application.Common.Exceptions;

/// <summary>
/// Throw khi user vượt quá giới hạn request (rate limiting).
/// GlobalExceptionMiddleware map sang HTTP 429 Too Many Requests.
/// </summary>
public sealed class TooManyRequestsException : Exception
{
    /// <summary>Số giây còn lại trước khi được thử lại (cho Retry-After header).</summary>
    public int? RetryAfterSeconds { get; }

    public TooManyRequestsException(string message) : base(message) { }

    public TooManyRequestsException(string message, int retryAfterSeconds)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}