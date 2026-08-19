using FluentValidation;
using SocialApp.Application.DTOs.Groups;

namespace SocialApp.Application.Validators.Groups;

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupDto>
{
    public CreateGroupValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên nhóm không được để trống.")
            .MaximumLength(100).WithMessage("Tên nhóm tối đa 100 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Mô tả nhóm tối đa 1000 ký tự.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Privacy)
            .IsInEnum().WithMessage("Loại nhóm không hợp lệ.");

        RuleFor(x => x.Avatar)
            .Must(f => f == null || f.Length <= 10 * 1024 * 1024)
            .WithMessage("Ảnh đại diện nhóm tối đa 10MB.");
    }
}

public sealed class UpdateGroupValidator : AbstractValidator<UpdateGroupDto>
{
    public UpdateGroupValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("Tên nhóm tối đa 100 ký tự.")
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Mô tả nhóm tối đa 1000 ký tự.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Avatar)
            .Must(f => f == null || f.Length <= 10 * 1024 * 1024)
            .WithMessage("Ảnh đại diện nhóm tối đa 10MB.")
            .When(x => x.Avatar is not null);

        RuleFor(x => x.Cover)
            .Must(f => f == null || f.Length <= 10 * 1024 * 1024)
            .WithMessage("Ảnh bìa nhóm tối đa 10MB.")
            .When(x => x.Cover is not null);
    }
}
