using System.Text.RegularExpressions;

namespace SocialApp.Application.Common.Helpers;

public static class FileNameSanitizer
{
    private static readonly Regex InvalidChars =
        new(@"[^a-zA-Z0-9._-]", RegexOptions.Compiled);

    private static readonly Regex MultipleUnderscores =
        new(@"_{2,}", RegexOptions.Compiled);

    private const int MaxLength = 100;

    public static string Sanitize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Guid.NewGuid().ToString();

        var ext = Path.GetExtension(fileName).ToLowerInvariant(); // vd: ".jpg"
        var nameOnly = Path.GetFileNameWithoutExtension(fileName);

        // Replace ký tự không hợp lệ → _
        var sanitized = InvalidChars.Replace(nameOnly, "_");

        // Collapse nhiều _ liên tiếp → 1
        sanitized = MultipleUnderscores.Replace(sanitized, "_");

        // Trim _ ở đầu/cuối
        sanitized = sanitized.Trim('_');

        // Nếu rỗng sau sanitize → dùng Guid
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = Guid.NewGuid().ToString();

        // Truncate: giữ lại MaxLength ký tự (bao gồm extension)
        var maxNameLength = MaxLength - ext.Length;
        if (sanitized.Length > maxNameLength)
            sanitized = sanitized[..maxNameLength];

        return sanitized + ext;
    }

    public static string GenerateUniqueFileName(string originalFileName)
    {
        var sanitized = Sanitize(originalFileName);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $"{timestamp}_{sanitized}";
    }
}