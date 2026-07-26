using FluentValidation;
using SocialApp.Application.DTOs.AI;

namespace SocialApp.Application.Validators.AI;

/// <summary>
/// Validator cho GeminiChatRequestDto.
/// </summary>
public sealed class GeminiChatRequestValidator : AbstractValidator<GeminiChatRequestDto>
{
    private static readonly string[] ValidRoles = ["user", "model"];

    public GeminiChatRequestValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
                .WithMessage("ConversationId không được để trống.")
            .Must(id => id != Guid.Empty)
                .WithMessage("ConversationId không hợp lệ.");

        RuleFor(x => x.NewMessage)
            .NotEmpty()
                .WithMessage("Tin nhắn không được để trống và tối đa 2000 ký tự.")
            .Must(m => !string.IsNullOrWhiteSpace(m))
                .WithMessage("Tin nhắn không được để trống và tối đa 2000 ký tự.")
            .MaximumLength(2000)
                .WithMessage("Tin nhắn không được để trống và tối đa 2000 ký tự.");

        // Validate từng item trong History nếu có
        RuleForEach(x => x.History)
            .ChildRules(item =>
            {
                item.RuleFor(m => m.Role)
                    .NotEmpty()
                        .WithMessage("Role trong history không được để trống.")
                    .Must(r => ValidRoles.Contains(r?.ToLower()))
                        .WithMessage("Role trong history chỉ được là 'user' hoặc 'model'.");

                item.RuleFor(m => m.Content)
                    .NotEmpty()
                        .WithMessage("Content trong history không được để trống.")
                    .Must(c => !string.IsNullOrWhiteSpace(c))
                        .WithMessage("Content trong history không được chỉ chứa khoảng trắng.");
            });
    }
}