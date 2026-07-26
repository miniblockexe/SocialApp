using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Friends;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Interface cho toàn bộ business logic liên quan đến kết bạn, block, gợi ý bạn bè.
/// </summary>
public interface IFriendService
{
    /// <summary>
    /// Gửi lời mời kết bạn từ sender đến receiver.
    /// Edge cases: cross request (B đã gửi cho A → auto accept),
    /// rejected trước đó (reuse record), đã block, đã là bạn...
    /// </summary>
    Task<FriendResponseDto> SendRequestAsync(Guid senderId, Guid receiverId);

    /// <summary>
    /// Chấp nhận lời mời kết bạn. Chỉ receiver mới có quyền accept.
    /// </summary>
    Task<FriendResponseDto> AcceptRequestAsync(Guid userId, Guid requestId);

    /// <summary>
    /// Từ chối lời mời kết bạn. Chỉ receiver mới có quyền reject.
    /// Không tạo notification để tránh lộ thông tin cho sender.
    /// </summary>
    Task<FriendResponseDto> RejectRequestAsync(Guid userId, Guid requestId);

    /// <summary>
    /// Hủy kết bạn với targetId. Hard delete record để có thể gửi lại request sau.
    /// </summary>
    Task UnfriendAsync(Guid userId, Guid targetId);

    /// <summary>
    /// Chặn targetId. Nếu đang là bạn hoặc có pending request → xóa trước rồi tạo block record.
    /// </summary>
    Task BlockUserAsync(Guid userId, Guid targetId);

    /// <summary>
    /// Bỏ chặn targetId. Hard delete block record.
    /// </summary>
    Task UnblockUserAsync(Guid userId, Guid targetId);

    /// <summary>
    /// Lấy danh sách bạn bè của userId, kèm số bạn chung với viewer.
    /// OrderBy FullName ASC.
    /// </summary>
    Task<PagedResult<FriendListItemDto>> GetFriendsAsync(Guid userId, int page, int size);

    /// <summary>
    /// Lấy danh sách lời mời kết bạn đang chờ xác nhận (userId là receiver).
    /// OrderBy CreatedAt DESC.
    /// </summary>
    Task<PagedResult<FriendResponseDto>> GetPendingRequestsAsync(Guid userId, int page, int size);

    /// <summary>
    /// Lấy danh sách lời mời kết bạn đã gửi đi (userId là sender, status = Pending).
    /// OrderBy CreatedAt DESC.
    /// </summary>
    Task<PagedResult<FriendResponseDto>> GetSentRequestsAsync(Guid userId, int page, int size);

    /// <summary>
    /// Gợi ý kết bạn dựa trên friends-of-friends.
    /// Loại trừ: chính userId, đã là bạn, đã có request, đã block hoặc bị block.
    /// Bổ sung user mới nhất nếu không đủ suggestion.
    /// </summary>
    Task<PagedResult<FriendSuggestionDto>> GetSuggestionsAsync(Guid userId, int page, int size);

    /// <summary>
    /// Lấy trạng thái quan hệ giữa userId và targetId.
    /// Trả FriendStatus — None = 99 nếu không có quan hệ.
    /// </summary>
    Task<FriendStatus> GetFriendshipStatusAsync(Guid userId, Guid targetId);

    /// <summary>
    /// Kiểm tra nhanh 2 user có phải bạn bè không.
    /// </summary>
    Task<bool> AreFriendsAsync(Guid userId1, Guid userId2);
}