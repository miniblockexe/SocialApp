using FluentValidation;
using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.Validators.Auth;

/// <summary>
/// FluentValidation validator cho <see cref="RegisterRequestDto"/>.
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    // Regex chỉ cho phép chữ cái, chữ số, dấu chấm và gạch dưới
    private static readonly System.Text.RegularExpressions.Regex UsernameRegex =
        new(@"^[a-zA-Z0-9._]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Regex kiểm tra password đủ độ phức tạp
    private static readonly System.Text.RegularExpressions.Regex UpperCaseRegex =
        new(@"[A-Z]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex LowerCaseRegex =
        new(@"[a-z]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex DigitRegex =
        new(@"[0-9]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex SpecialCharRegex =
        new(@"[^a-zA-Z0-9]", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Regex kiểm tra chuỗi toàn số
    private static readonly System.Text.RegularExpressions.Regex AllDigitsRegex =
        new(@"^\d+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public RegisterRequestValidator()
    {
        // Username
        RuleFor(x => x.Username)
            .NotEmpty()
                .WithMessage("Tên đăng nhập không được để trống.")
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithMessage("Tên đăng nhập không được chỉ chứa khoảng trắng.")
            .Length(3, 50)
                .WithMessage("Tên đăng nhập phải từ 3 đến 50 ký tự.")
            .Must(v => UsernameRegex.IsMatch(v))
                .WithMessage("Tên đăng nhập chỉ được chứa chữ cái, chữ số, dấu chấm (.) và gạch dưới (_).")
            .Must(v => !AllDigitsRegex.IsMatch(v))
                .WithMessage("Tên đăng nhập không được chỉ toàn là số.");

        // Email
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage("Email không được để trống.")
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithMessage("Email không được chỉ chứa khoảng trắng.")
            .EmailAddress()
                .WithMessage("Email không đúng định dạng.")
            .MaximumLength(256)
                .WithMessage("Email không được vượt quá 256 ký tự.");

        // Password
        RuleFor(x => x.Password)
            .NotEmpty()
                .WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8)
                .WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .MaximumLength(128)
                .WithMessage("Mật khẩu không được vượt quá 128 ký tự.")
            .Must(v => UpperCaseRegex.IsMatch(v))
                .WithMessage("Mật khẩu phải chứa ít nhất 1 chữ hoa.")
            .Must(v => LowerCaseRegex.IsMatch(v))
                .WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường.")
            .Must(v => DigitRegex.IsMatch(v))
                .WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.")
            .Must(v => SpecialCharRegex.IsMatch(v))
                .WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");

        // ConfirmPassword
        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
                .WithMessage("Xác nhận mật khẩu không được để trống.")
            .Equal(x => x.Password)
                .WithMessage("Mật khẩu xác nhận không khớp.");

        // FullName
        RuleFor(x => x.FullName)
            .NotEmpty()
                .WithMessage("Họ tên không được để trống.")
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithMessage("Họ tên không được chỉ chứa khoảng trắng.")
            .Length(2, 100)
                .WithMessage("Họ tên phải từ 2 đến 100 ký tự.")
            .Must(v => !AllDigitsRegex.IsMatch(v.Trim()))
                .WithMessage("Họ tên không được chỉ toàn là số.");
    }
}