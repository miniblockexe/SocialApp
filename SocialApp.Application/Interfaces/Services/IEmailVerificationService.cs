namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Xác thực email tồn tại thật (Mailboxlayer API).
/// Fail-open: trả null nếu API disabled hoặc có lỗi — không chặn đăng ký.
/// </summary>
public interface IEmailVerificationService
{
    Task<EmailVerificationResult?> VerifyAsync(string email, CancellationToken ct = default);
}

/// <summary>Kết quả xác thực email từ Mailboxlayer.</summary>
public sealed record EmailVerificationResult(
    bool FormatValid,
    bool SmtpValid,
    bool IsDisposable);
