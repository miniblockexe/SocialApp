using FluentValidation;
using SocialApp.Application.DTOs.Users;

namespace SocialApp.Application.Validators.Users;

/// <summary>
/// FluentValidation validator cho <see cref="PrivacySettingsDto"/>.
/// Đảm bảo mỗi field chỉ nhận giá trị enum PostPrivacy hợp lệ (Public/Friends/OnlyMe).
/// </summary>
public sealed class PrivacySettingsValidator : AbstractValidator<PrivacySettingsDto>
{
    public PrivacySettingsValidator()
    {
        RuleFor(x => x.ProfileVisibility)
            .IsInEnum().WithMessage("ProfileVisibility không hợp lệ.");

        RuleFor(x => x.PostVisibility)
            .IsInEnum().WithMessage("PostVisibility không hợp lệ.");

        RuleFor(x => x.FriendListVisible)
            .IsInEnum().WithMessage("FriendListVisible không hợp lệ.");

        RuleFor(x => x.SearchDiscoverable)
            .IsInEnum().WithMessage("SearchDiscoverable không hợp lệ.");
    }
}