using FluentValidation;
using SocialApp.Application.DTOs.Admin;

namespace SocialApp.Application.Validators.Admin;

/// <summary>
/// Validator cho AdminDeletePostDto — đảm bảo lý do xóa bài đăng hợp lệ.
/// </summary>
public sealed class AdminDeletePostValidator : AbstractValidator<AdminDeletePostDto>
{
    public AdminDeletePostValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
                .WithMessage("Lý do xóa bài đăng không được để trống.")
            .Must(r => !string.IsNullOrWhiteSpace(r))
                .WithMessage("Lý do xóa bài đăng không được chỉ chứa khoảng trắng.")
            .MinimumLength(5)
                .WithMessage("Lý do xóa bài đăng phải từ 5-500 ký tự.")
            .MaximumLength(500)
                .WithMessage("Lý do xóa bài đăng phải từ 5-500 ký tự.");
    }
}