namespace SocialApp.Application.Common.Helpers;

public static class MagicBytesValidator
{
    public static bool IsValidJpeg(byte[] header)
        => header.Length >= 3
        && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

    public static bool IsValidPng(byte[] header)
        => header.Length >= 8
        && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
        && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

    public static bool IsValidGif(byte[] header)
        => header.Length >= 4
        && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38;

    public static bool IsValidWebp(byte[] header)
        => header.Length >= 12
        && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
        && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

    public static bool IsValidMp4(byte[] header)
        => header.Length >= 8
        && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;

    public static bool IsValidWebm(byte[] header)
        => header.Length >= 4
        && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;

    public static bool IsValidMp3(byte[] header)
        => header.Length >= 3
        && ((header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)   // ID3
        || (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0));               // frame sync

    public static bool IsValidWav(byte[] header)
        => header.Length >= 12
        && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
        && header[8] == 0x57 && header[9] == 0x41 && header[10] == 0x56 && header[11] == 0x45;

    public static bool IsValidOgg(byte[] header)
        => header.Length >= 4
        && header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53;

    public static bool Validate(byte[] header, string contentType) => contentType switch
    {
        "image/jpeg" => IsValidJpeg(header),
        "image/png" => IsValidPng(header),
        "image/gif" => IsValidGif(header),
        "image/webp" => IsValidWebp(header),
        "video/mp4" => IsValidMp4(header),
        "video/webm" => IsValidWebm(header),
        "video/quicktime" => IsValidMp4(header),   // MOV dùng ftyp box giống MP4
        "audio/mpeg" => IsValidMp3(header),
        "audio/wav" => IsValidWav(header),
        "audio/ogg" => IsValidOgg(header),
        "audio/mp4" => IsValidMp4(header),   // M4A cũng dùng ftyp box
        _ => false
    };
}