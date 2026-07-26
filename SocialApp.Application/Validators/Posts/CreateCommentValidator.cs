using FluentValidation;
using SocialApp.Application.DTOs.Posts;

namespace SocialApp.Application.Validators.Posts;

/// <summary>
/// FluentValidation validator cho <see cref="CreateCommentDto"/>.
/// </summary>
public sealed class CreateCommentValidator : AbstractValidator<CreateCommentDto>
{
    private const int MaxContentLength = 2000;

    public CreateCommentValidator()
    {
        // NotEmpty() trên string đã tự reject null / "" / whitespace-only trong FluentValidation,
        // nên không cần thêm .Must(IsNullOrWhiteSpace) riêng — tránh rule trùng lặp.
        RuleFor(x => x.Content)
            .NotEmpty()
                .WithMessage("Nội dung bình luận không được để trống.")
            .MaximumLength(MaxContentLength)
                .WithMessage($"Nội dung bình luận không được vượt quá {MaxContentLength} ký tự.");
    }
}