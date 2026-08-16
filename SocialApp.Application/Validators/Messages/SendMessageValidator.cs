using FluentValidation;
using SocialApp.Application.DTOs.Messages;

namespace SocialApp.Application.Validators.Messages;

/// <summary>
/// Validator cho SendMessageDto (HTTP multipart/form-data).
/// Bắt buộc phải có Content HOẶC Attachment — không được cả 2 đều null.
/// Content sau Trim không được rỗng nếu Attachment null.
/// </summary>
public sealed class SendMessageValidator : AbstractValidator<SendMessageDto>
{
    public SendMessageValidator()
    {
        // ConversationId bắt buộc, không được Guid.Empty
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("ConversationId không được để trống.")
            .Must(id => id != Guid.Empty)
            .WithMessage("ConversationId không hợp lệ.");

        // Content nếu có: không toàn whitespace, tối đa 4000 ký tự
        When(x => x.Content is not null, () =>
        {
            RuleFor(x => x.Content)
                .Must(c => !string.IsNullOrWhiteSpace(c))
                .WithMessage("Nội dung tin nhắn không được chỉ chứa khoảng trắng.")
                .MaximumLength(4000)
                .WithMessage("Nội dung tin nhắn không được vượt quá 4000 ký tự.");
        });

        RuleFor(x => x)
            .Must(dto =>
                !string.IsNullOrWhiteSpace(dto.Content) ||
                dto.Attachment is not null ||
                dto.GifUrl is not null ||
                dto.SharedPostId.HasValue)
            .WithMessage("Tin nhắn phải có nội dung, file đính kèm, GIF hoặc bài viết chia sẻ.")
            .OverridePropertyName("Content");

        When(x => x.Attachment is null && !x.SharedPostId.HasValue && x.GifUrl is null, () =>
        {
            RuleFor(x => x.Content)
                .NotNull()
                .WithMessage("Nội dung tin nhắn không được để trống khi không có file đính kèm.")
                .Must(c => !string.IsNullOrWhiteSpace(c))
                .WithMessage("Nội dung tin nhắn không được chỉ chứa khoảng trắng.");
        });
    }
}