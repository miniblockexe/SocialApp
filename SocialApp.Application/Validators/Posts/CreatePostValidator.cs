using FluentValidation;
using SocialApp.Application.DTOs.Posts;

namespace SocialApp.Application.Validators.Posts;

/// <summary>
/// FluentValidation validator cho <see cref="CreatePostDto"/>.
/// Validate cấu trúc (độ dài, số lượng, enum hợp lệ) — magic bytes/ContentType từng file
/// do PostService validate sâu hơn khi upload (giống pattern UserService.ValidateImageFile).
/// </summary>
public sealed class CreatePostValidator : AbstractValidator<CreatePostDto>
{
    private const int MaxContentLength = 5000;
    private const int MaxMediaFiles = 10;
    private const long MaxMediaFileBytes = 500 * 1024 * 1024; // 500MB — chặn nhanh input quá khổ;
    // giới hạn CHÍNH XÁC theo từng loại (ảnh/video/audio) do PostService kiểm tra sâu hơn
    // qua FileValidationSettings/CloudflareR2Settings (200MB ảnh, 500MB video, 50MB audio).

    public CreatePostValidator()
    {
        // Content
        // Chỉ validate khi client gửi lên (không null). Gửi "" coi như invalid —
        // muốn bỏ trống Content (post chỉ có media) thì gửi null, không gửi "".
        When(x => x.Content is not null, () =>
        {
            RuleFor(x => x.Content!)
                .Must(v => !string.IsNullOrWhiteSpace(v))
                    .WithMessage("Content không được chỉ chứa khoảng trắng.")
                .MaximumLength(MaxContentLength)
                    .WithMessage($"Content không được vượt quá {MaxContentLength} ký tự.");
        });

        // Content HOẶC MediaFiles
        RuleFor(x => x)
            .Must(dto =>
                !string.IsNullOrWhiteSpace(dto.Content) ||
                (dto.MediaFiles is not null && dto.MediaFiles.Count > 0))
            .WithMessage("Bài đăng phải có nội dung hoặc ít nhất 1 file media.")
            .WithName("Content");

        // MediaFiles
        When(x => x.MediaFiles is not null && x.MediaFiles.Count > 0, () =>
        {
            RuleFor(x => x.MediaFiles!)
                .Must(files => files.Count <= MaxMediaFiles)
                    .WithMessage($"Tối đa {MaxMediaFiles} file media mỗi bài đăng.");

            RuleForEach(x => x.MediaFiles!)
                .Must(file => file.Length <= MaxMediaFileBytes)
                    .WithMessage("Mỗi file media không được vượt quá 500MB.");
        });

        // Privacy
        RuleFor(x => x.Privacy)
            .IsInEnum()
                .WithMessage("Privacy không hợp lệ.");
    }
}