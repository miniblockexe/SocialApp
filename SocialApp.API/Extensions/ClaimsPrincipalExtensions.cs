using System.Security.Claims;

namespace SocialApp.API.Extensions;

/// <summary>
/// Extension methods cho ClaimsPrincipal — dùng trong Controller/Middleware
/// để lấy thông tin user từ JWT mà không cần viết lại boilerplate.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    
    // Identity
    

    /// <summary>
    /// Lấy UserId (Guid) từ claim "sub".
    /// Trả Guid.Empty nếu không tìm thấy hoặc parse thất bại.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Lấy UserId và throw UnauthorizedAccessException nếu không có hoặc là Guid.Empty.
    /// Dùng trong các endpoint bắt buộc phải có userId hợp lệ.
    /// </summary>
    public static Guid GetUserIdOrThrow(this ClaimsPrincipal user)
    {
        var id = user.GetUserId();
        if (id == Guid.Empty)
            throw new UnauthorizedAccessException("Không xác định được danh tính người dùng.");
        return id;
    }

    /// <summary>
    /// Lấy Email từ claim. Null nếu không tìm thấy.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLower();
    }

    /// <summary>
    /// Lấy Username từ claim "name" hoặc "preferred_username".
    /// Null nếu không tìm thấy.
    /// </summary>
    public static string? GetUsername(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("name");

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Lấy DisplayName (full name) từ claim "given_name" + "family_name"
    /// hoặc fallback về username.
    /// </summary>
    public static string? GetDisplayName(this ClaimsPrincipal user)
    {
        var given = user.FindFirstValue(ClaimTypes.GivenName) ?? user.FindFirstValue("given_name");
        var family = user.FindFirstValue(ClaimTypes.Surname) ?? user.FindFirstValue("family_name");

        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(family))
            return $"{given} {family}".Trim();

        return user.GetUsername();
    }

    
    // Roles & Permissions
    

    /// <summary>
    /// Lấy danh sách tất cả roles của user.
    /// </summary>
    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Kiểm tra user có role cụ thể không (case-insensitive).
    /// </summary>
    public static bool HasRole(this ClaimsPrincipal user, string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return user.IsInRole(role.Trim());
    }

    /// <summary>
    /// Kiểm tra user có ít nhất 1 trong các roles không.
    /// </summary>
    public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles)
        => roles.Any(user.HasRole);

    /// <summary>
    /// Kiểm tra user là Admin không.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal user)
        => user.HasRole("Admin");

    /// <summary>
    /// Kiểm tra user là Moderator hoặc Admin.
    /// </summary>
    public static bool IsModerator(this ClaimsPrincipal user)
        => user.HasAnyRole("Admin", "Moderator");

    
    // Token metadata
    

    /// <summary>
    /// Lấy JWT ID (jti claim) — dùng để revoke token cụ thể.
    /// </summary>
    public static string? GetJwtId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("jti");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Lấy thời điểm token được cấp (iat claim). Null nếu không có.
    /// </summary>
    public static DateTime? GetIssuedAt(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("iat");
        if (long.TryParse(value, out var unixTime))
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
        return null;
    }

    
    // Authorization helpers
    

    /// <summary>
    /// Kiểm tra request có phải từ chính user đó không (tự thao tác trên resource của mình).
    /// Dùng để ngăn user A thao tác trên resource của user B.
    /// </summary>
    public static bool IsResourceOwner(this ClaimsPrincipal user, Guid resourceOwnerId)
    {
        if (resourceOwnerId == Guid.Empty) return false;
        return user.GetUserId() == resourceOwnerId;
    }

    /// <summary>
    /// Kiểm tra user có quyền thao tác trên resource không:
    /// là chủ sở hữu HOẶC là Admin/Moderator.
    /// </summary>
    public static bool CanAccessResource(this ClaimsPrincipal user, Guid resourceOwnerId)
        => user.IsResourceOwner(resourceOwnerId) || user.IsModerator();
}