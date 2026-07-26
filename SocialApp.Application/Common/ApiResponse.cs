namespace SocialApp.Application.Common;

/// <summary>
/// Wrapper chuẩn cho mọi response của API.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của data trả về.</typeparam>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    // Private constructor — bắt buộc dùng factory methods

    private ApiResponse() { }

    // 2xx

    /// <summary>200 OK — trả data kèm message tuỳ chọn.</summary>
    public static ApiResponse<T> Ok(T data, string message = "Thành công.")
        => new() { Success = true, Message = message, Data = data };

    /// <summary>200 OK — không có data (dùng cho thao tác không cần trả body).</summary>
    public static ApiResponse<T> Ok(string message = "Thành công.")
        => new() { Success = true, Message = message };

    /// <summary>201 Created.</summary>
    public static ApiResponse<T> Created(T data, string message = "Tạo mới thành công.")
        => new() { Success = true, Message = message, Data = data };

    // 4xx

    /// <summary>400 Bad Request — input sai / body rỗng / string whitespace.</summary>
    public static ApiResponse<T> BadRequest(string message = "Dữ liệu đầu vào không hợp lệ.")
        => new() { Success = false, Message = message };

    /// <summary>400 Bad Request — kèm danh sách lỗi chi tiết.</summary>
    public static ApiResponse<T> BadRequest(IEnumerable<string> errors, string message = "Dữ liệu đầu vào không hợp lệ.")
        => new() { Success = false, Message = message, Errors = errors.ToList().AsReadOnly() };

    /// <summary>401 Unauthorized — chưa đăng nhập hoặc token không hợp lệ.</summary>
    public static ApiResponse<T> Unauthorized(string message = "Bạn cần đăng nhập để thực hiện thao tác này.")
        => new() { Success = false, Message = message };

    /// <summary>403 Forbidden — đã đăng nhập nhưng không có quyền.</summary>
    public static ApiResponse<T> Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.")
        => new() { Success = false, Message = message };

    /// <summary>404 Not Found.</summary>
    public static ApiResponse<T> NotFound(string message = "Không tìm thấy dữ liệu.")
        => new() { Success = false, Message = message };

    /// <summary>409 Conflict — duplicate resource (email, username...).</summary>
    public static ApiResponse<T> Conflict(string message = "Dữ liệu đã tồn tại.")
        => new() { Success = false, Message = message };

    /// <summary>413 Payload Too Large — file vượt quá giới hạn.</summary>
    public static ApiResponse<T> PayloadTooLarge(string message = "File vượt quá kích thước cho phép.")
        => new() { Success = false, Message = message };

    /// <summary>422 Unprocessable Entity — FluentValidation errors.</summary>
    public static ApiResponse<T> UnprocessableEntity(IEnumerable<string> errors, string message = "Dữ liệu không hợp lệ.")
        => new() { Success = false, Message = message, Errors = errors.ToList().AsReadOnly() };

    /// <summary>429 Too Many Requests.</summary>
    public static ApiResponse<T> TooManyRequests(string message = "Bạn thực hiện quá nhiều yêu cầu. Vui lòng thử lại sau.")
        => new() { Success = false, Message = message };

    // 5xx

    /// <summary>
    /// 500 Internal Server Error.
    /// KHÔNG bao giờ trả stack trace hay message gốc của exception ra client.
    /// </summary>
    public static ApiResponse<T> InternalServerError(string message = "Đã xảy ra lỗi, vui lòng thử lại sau.")
        => new() { Success = false, Message = message };
}

/// <summary>
/// Non-generic version — dùng khi không có data cần trả (DELETE, PUT 204...).
/// </summary>
public sealed class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = [];

    private ApiResponse() { }

    public static ApiResponse Ok(string message = "Thành công.")
        => new() { Success = true, Message = message };

    public static ApiResponse BadRequest(string message = "Dữ liệu đầu vào không hợp lệ.")
        => new() { Success = false, Message = message };

    public static ApiResponse BadRequest(IEnumerable<string> errors, string message = "Dữ liệu đầu vào không hợp lệ.")
        => new() { Success = false, Message = message, Errors = errors.ToList().AsReadOnly() };

    public static ApiResponse Unauthorized(string message = "Bạn cần đăng nhập để thực hiện thao tác này.")
        => new() { Success = false, Message = message };

    public static ApiResponse Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.")
        => new() { Success = false, Message = message };

    public static ApiResponse NotFound(string message = "Không tìm thấy dữ liệu.")
        => new() { Success = false, Message = message };

    public static ApiResponse Conflict(string message = "Dữ liệu đã tồn tại.")
        => new() { Success = false, Message = message };

    public static ApiResponse UnprocessableEntity(IEnumerable<string> errors, string message = "Dữ liệu không hợp lệ.")
        => new() { Success = false, Message = message, Errors = errors.ToList().AsReadOnly() };

    public static ApiResponse TooManyRequests(string message = "Bạn thực hiện quá nhiều yêu cầu. Vui lòng thử lại sau.")
        => new() { Success = false, Message = message };

    public static ApiResponse InternalServerError(string message = "Đã xảy ra lỗi, vui lòng thử lại sau.")
        => new() { Success = false, Message = message };
}