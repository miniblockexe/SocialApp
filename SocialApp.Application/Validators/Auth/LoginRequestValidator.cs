using FluentValidation;
using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.Validators.Auth;

/// <summary>
/// FluentValidation validator cho <see cref="LoginRequestDto"/>.
/// Chỉ validate format cơ bản — không kiểm tra email/password đúng/sai ở đây
/// (logic đó thuộc về AuthService để tránh lộ thông tin qua validation message).
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        // Email
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage("Email không được để trống.")
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithMessage("Email không được chỉ chứa khoảng trắng.")
            .EmailAddress()
                .WithMessage("Email không đúng định dạng.");

        // Password
        RuleFor(x => x.Password)
            .NotEmpty()
                .WithMessage("Mật khẩu không được để trống.")
            .MaximumLength(128)
                .WithMessage("Mật khẩu không được vượt quá 128 ký tự.");
    }
}