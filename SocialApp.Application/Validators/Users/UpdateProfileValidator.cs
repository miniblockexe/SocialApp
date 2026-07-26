using FluentValidation;
using SocialApp.Application.DTOs.Users;

namespace SocialApp.Application.Validators.Users;

/// <summary>
/// FluentValidation validator cho <see cref="UpdateProfileDto"/>.
/// Cả 2 field đều optional — chỉ validate khi có giá trị.
/// </summary>
public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileDto>
{
    // Regex kiểm tra chuỗi toàn số
    private static readonly System.Text.RegularExpressions.Regex AllDigitsRegex =
        new(@"^\d+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public UpdateProfileValidator()
    {
        // FullName
        // Chỉ validate khi client gửi lên (không null)
        When(x => x.FullName is not null, () =>
        {
            RuleFor(x => x.FullName!)
                .Must(v => !string.IsNullOrWhiteSpace(v))
                    .WithMessage("Họ tên không được chỉ chứa khoảng trắng.")
                .Length(2, 100)
                    .WithMessage("Họ tên phải từ 2 đến 100 ký tự.")
                .Must(v => !AllDigitsRegex.IsMatch(v.Trim()))
                    .WithMessage("Họ tên không được chỉ toàn là số.");
        });

        // Bio
        // Chỉ validate khi client gửi lên (không null)
        // Bio có thể là empty string → service xử lý set về null (xóa bio)
        When(x => x.Bio is not null, () =>
        {
            RuleFor(x => x.Bio!)
                .MaximumLength(500)
                    .WithMessage("Giới thiệu bản thân không được vượt quá 500 ký tự.");
        });
    }
}