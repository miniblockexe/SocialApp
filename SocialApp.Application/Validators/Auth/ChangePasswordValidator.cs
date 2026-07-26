using FluentValidation;
using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.Validators.Auth;

/// <summary>
/// FluentValidation validator cho <see cref="ChangePasswordDto"/>.
/// </summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
    private static readonly System.Text.RegularExpressions.Regex UpperCaseRegex =
        new(@"[A-Z]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex LowerCaseRegex =
        new(@"[a-z]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex DigitRegex =
        new(@"[0-9]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex SpecialCharRegex =
        new(@"[^a-zA-Z0-9]", System.Text.RegularExpressions.RegexOptions.Compiled);

    public ChangePasswordValidator()
    {
        // OldPassword
        RuleFor(x => x.OldPassword)
            .NotEmpty()
                .WithMessage("Mật khẩu cũ không được để trống.");

        // NewPassword
        RuleFor(x => x.NewPassword)
            .NotEmpty()
                .WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(8)
                .WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .MaximumLength(128)
                .WithMessage("Mật khẩu mới không được vượt quá 128 ký tự.")
            .Must(v => UpperCaseRegex.IsMatch(v))
                .WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ hoa.")
            .Must(v => LowerCaseRegex.IsMatch(v))
                .WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ thường.")
            .Must(v => DigitRegex.IsMatch(v))
                .WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ số.")
            .Must(v => SpecialCharRegex.IsMatch(v))
                .WithMessage("Mật khẩu mới phải chứa ít nhất 1 ký tự đặc biệt.")
            .Must((dto, newPwd) => newPwd != dto.OldPassword)
                .WithMessage("Mật khẩu mới không được trùng mật khẩu cũ.");

        // ConfirmNewPassword
        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty()
                .WithMessage("Xác nhận mật khẩu mới không được để trống.")
            .Equal(x => x.NewPassword)
                .WithMessage("Mật khẩu xác nhận không khớp.");
    }
}