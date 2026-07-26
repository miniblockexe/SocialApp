using FluentValidation;
using SocialApp.Application.DTOs.Admin;

namespace SocialApp.Application.Validators.Admin;

/// <summary>
/// Validator cho BanUserDto — đảm bảo lý do cấm tài khoản hợp lệ.
/// </summary>
public sealed class BanUserValidator : AbstractValidator<BanUserDto>
{
    public BanUserValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
                .WithMessage("Lý do cấm tài khoản phải từ 10-500 ký tự.")
            .Must(r => !string.IsNullOrWhiteSpace(r))
                .WithMessage("Lý do cấm tài khoản không được chỉ chứa khoảng trắng.")
            .MinimumLength(10)
                .WithMessage("Lý do cấm tài khoản phải từ 10-500 ký tự.")
            .MaximumLength(500)
                .WithMessage("Lý do cấm tài khoản phải từ 10-500 ký tự.");
    }
}