using FluentValidation;
using SocialApp.Application.DTOs.Messages;

namespace SocialApp.Application.Validators.Messages;

/// <summary>
/// Validator cho CreateConversationDto.
/// Phân biệt rõ 2 case: 1-1 và group, với rules khác nhau.
/// Check Guid.Empty và duplicate trong ParticipantIds.
/// </summary>
public sealed class CreateConversationValidator : AbstractValidator<CreateConversationDto>
{
    public CreateConversationValidator()
    {
        // ParticipantIds không được null/rỗng
        RuleFor(x => x.ParticipantIds)
            .NotNull()
            .WithMessage("Danh sách người tham gia không được để trống.")
            .NotEmpty()
            .WithMessage("Phải có ít nhất 1 người tham gia.");

        // Không được có Guid.Empty trong danh sách
        RuleForEach(x => x.ParticipantIds)
            .NotEqual(Guid.Empty)
            .WithMessage("Id người dùng không hợp lệ.");

        // Không được có duplicate trong ParticipantIds
        RuleFor(x => x.ParticipantIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Danh sách người tham gia không được có Id trùng lặp.");

        // Rules riêng cho conversation 1-1
        When(x => !x.IsGroup, () =>
        {
            RuleFor(x => x.ParticipantIds)
                .Must(ids => ids is not null && ids.Count == 1)
                .WithMessage("Conversation 1-1 chỉ được có đúng 1 người tham gia.");
        });

        // Rules riêng cho group
        When(x => x.IsGroup, () =>
        {
            RuleFor(x => x.ParticipantIds)
                .Must(ids => ids is not null && ids.Count >= 2)
                .WithMessage("Group conversation phải có ít nhất 2 người tham gia.");

            RuleFor(x => x.GroupName)
                .NotNull()
                .WithMessage("Tên group không được để trống.")
                .NotEmpty()
                .WithMessage("Tên group không được để trống.")
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("Tên group không được chỉ chứa khoảng trắng.")
                .MinimumLength(2)
                .WithMessage("Tên group phải có ít nhất 2 ký tự.")
                .MaximumLength(100)
                .WithMessage("Tên group không được vượt quá 100 ký tự.")
                .When(x => x.GroupName is not null);
        });
    }
}