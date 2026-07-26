namespace SocialApp.Application.Common.Exceptions;

/// <summary>
/// Throw cho các lỗi business logic (vi phạm quy tắc nghiệp vụ).
/// GlobalExceptionMiddleware map sang HTTP 400 Bad Request.
/// Dùng thay thế InvalidOperationException để tránh bắt nhầm lỗi system.
/// Ví dụ: bài viết đã xóa, token đã revoke, lời mời không còn hiệu lực...
/// </summary>
public sealed class BusinessException : Exception
{
    /// <summary>Mã lỗi nội bộ (tùy chọn) để controller xử lý đặc biệt nếu cần.</summary>
    public string? ErrorCode { get; }

    public BusinessException(string message) : base(message) { }

    public BusinessException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public BusinessException(string message, Exception innerException)
        : base(message, innerException) { }
}