using FluentValidation;
using SocialApp.Application.DTOs.Friends;

namespace SocialApp.Application.Validators.Friends;

/// <summary>
/// Validator cho FriendRequestDto.
/// Chỉ validate input cơ bản — business logic (không gửi cho chính mình,
/// kiểm tra block, đã là bạn...) xử lý trong FriendService.
/// </summary>
public sealed class FriendRequestValidator : AbstractValidator<FriendRequestDto>
{
    public FriendRequestValidator()
    {
        RuleFor(x => x.ReceiverId)
            .NotEmpty()
                .WithMessage("ReceiverId không được để trống.")
            .Must(id => id != Guid.Empty)
                .WithMessage("ReceiverId không hợp lệ.");
    }
}