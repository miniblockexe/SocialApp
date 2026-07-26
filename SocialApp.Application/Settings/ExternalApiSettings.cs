namespace SocialApp.Application.Settings;

/// <summary>Mailboxlayer — xác thực email khi đăng ký (free 100 req/tháng).</summary>
public sealed class MailboxlayerSettings
{
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>Tắt trong môi trường dev để tiết kiệm quota.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Từ chối disposable email (mailinator, temp-mail, …).</summary>
    public bool BlockDisposable { get; init; } = true;
}

/// <summary>TinyURL — rút gọn link chia sẻ bài viết (free, không cần key).</summary>
public sealed class TinyUrlSettings
{
    /// <summary>API key tùy chọn — dùng nếu muốn custom alias hoặc Pro plan.</summary>
    public string ApiKey { get; init; } = string.Empty;
}

/// <summary>Tenor — GIF search/trending trong chat (free 10k req/ngày).</summary>
public sealed class TenorSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public int DefaultLimit { get; init; } = 20;
    public string Locale { get; init; } = "vi_VN";
}
