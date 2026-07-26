namespace SocialApp.Application.Common.Exceptions;

/// <summary>
/// Throw khi user đã xác thực nhưng không có quyền thực hiện thao tác.
/// GlobalExceptionMiddleware map sang HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException) { }
}