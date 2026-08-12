using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Posts;
using SocialApp.Application.Services;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Contract cho Post Service.
/// Xử lý CRUD bài đăng, feed, toggle like, comment (kèm reply 1 cấp).
/// Quy ước exception → HTTP status (theo GlobalExceptionMiddleware):
///   ArgumentException → 400, KeyNotFoundException → 404,
///   ForbiddenException → 403 (KHÔNG dùng UnauthorizedAccessException — cái đó map 401),
///   InvalidOperationException → 400 (state conflict, vd "đã xóa trước đó") hoặc 500 (lỗi hệ thống).
/// </summary>
public interface IPostService
{
    /// <summary>
    /// Tạo bài đăng mới. Phải có Content hoặc MediaFiles (đã được validator chặn trước,
    /// nhưng service vẫn tự kiểm tra lại — defensive coding).
    /// Nếu upload media thất bại giữa chừng → rollback (xóa file đã upload thành công + xóa post vừa tạo).
    /// </summary>
    /// <param name="userId">Id tác giả.</param>
    /// <param name="dto">Nội dung, quyền hiển thị, file đính kèm.</param>
    /// <exception cref="ArgumentException">400 — Content và MediaFiles đều rỗng.</exception>
    /// <exception cref="InvalidOperationException">500 — upload cloud thất bại sau khi đã rollback.</exception>
    Task<PostResponseDto> CreatePostAsync(Guid userId, CreatePostDto dto);

    /// <summary>
    /// Cập nhật Content/Privacy của bài đăng. Không cho thêm/xóa media qua endpoint này
    /// (media quản lý riêng, đơn giản hóa).
    /// </summary>
    /// <param name="userId">Id user đang thao tác.</param>
    /// <param name="postId">Id bài đăng cần sửa.</param>
    /// <param name="dto">Field nào có giá trị được cập nhật, null giữ nguyên.</param>
    /// <exception cref="KeyNotFoundException">404 — bài không tồn tại hoặc đã xóa.</exception>
    /// <exception cref="ForbiddenException">403 — không phải chủ bài viết.</exception>
    Task<PostResponseDto> UpdatePostAsync(Guid userId, Guid postId, UpdatePostDto dto);

    /// <summary>
    /// Soft-delete bài đăng. Không xóa media trên cloud (giữ lại phục vụ audit).
    /// </summary>
    /// <param name="userId">Id user đang thao tác.</param>
    /// <param name="postId">Id bài đăng cần xóa.</param>
    /// <param name="isAdmin">True nếu admin xóa (bỏ qua check ownership, log thêm adminId + lý do).</param>
    /// <exception cref="KeyNotFoundException">404 — bài không tồn tại.</exception>
    /// <exception cref="ForbiddenException">403 — không phải chủ bài viết và không phải admin.</exception>
    /// <exception cref="InvalidOperationException">400 — bài đã được xóa trước đó.</exception>
    Task DeletePostAsync(Guid userId, Guid postId, bool isAdmin = false);

    /// <summary>
    /// Lấy chi tiết 1 bài đăng theo góc nhìn viewer (áp dụng privacy check).
    /// OnlyMe không đủ quyền xem → 404 (không phải 403, tránh lộ bài viết có tồn tại hay không).
    /// Friends không đủ quyền xem → 403 (biết bài tồn tại nhưng không xem được).
    /// </summary>
    /// <param name="postId">Id bài đăng.</param>
    /// <param name="viewerId">Id user đang xem.</param>
    /// <exception cref="KeyNotFoundException">404 — không tồn tại, hoặc OnlyMe và viewer không phải tác giả.</exception>
    /// <exception cref="ForbiddenException">403 — Friends-only và viewer không phải bạn/tác giả.</exception>
    Task<PostResponseDto> GetPostByIdAsync(Guid postId, Guid viewerId);

    /// <summary>
    /// Lấy feed: bài của bạn bè (trừ OnlyMe) + bài của chính mình (mọi privacy)
    /// + bài Public của người lạ. Loại bỏ bài của user đã block mình.
    /// Có CursorId → lấy bài cũ hơn cursor (hiệu quả hơn OFFSET).
    /// </summary>
    /// <param name="userId">Id user đang xem feed.</param>
    /// <param name="query">Page/Size/CursorId — đã tự clamp trong FeedQueryDto.</param>
    Task<PagedResult<PostResponseDto>> GetFeedAsync(Guid userId, FeedQueryDto query);

    /// <summary>
    /// Lấy danh sách bài đăng của 1 user cụ thể (áp dụng privacy check theo viewer,
    /// tương tự GetPostByIdAsync nhưng lọc ở mức danh sách thay vì throw).
    /// </summary>
    /// <param name="targetId">Id chủ trang cần xem bài.</param>
    /// <param name="viewerId">Id user đang xem.</param>
    /// <param name="page">Trang hiện tại.</param>
    /// <param name="size">Số kết quả mỗi trang.</param>
    Task<PagedResult<PostResponseDto>> GetUserPostsAsync(
        Guid targetId, Guid viewerId, int page, int size);

    /// <summary>
    /// Toggle like — đã like thì unlike, chưa like thì like (không throw khi toggle trùng).
    /// Nếu vừa like (không phải unlike) và không phải tự like bài mình → tạo notification.
    /// </summary>
    /// <param name="userId">Id user thực hiện.</param>
    /// <param name="postId">Id bài đăng.</param>
    /// <returns>True nếu vừa like, false nếu vừa unlike.</returns>
    /// <exception cref="KeyNotFoundException">404 — bài không tồn tại hoặc OnlyMe không đủ quyền.</exception>
    /// <exception cref="ForbiddenException">403 — Friends-only và không đủ quyền xem bài.</exception>
    Task<bool> ToggleLikeAsync(Guid userId, Guid postId);

    /// <summary>Lấy danh sách bình luận gốc của 1 bài đăng (kèm RepliesCount mỗi comment).</summary>
    /// <param name="postId">Id bài đăng.</param>
    /// <param name="viewerId">Id user đang xem.</param>
    /// <param name="page">Trang hiện tại.</param>
    /// <param name="size">Số kết quả mỗi trang.</param>
    /// <exception cref="KeyNotFoundException">404 — bài không tồn tại hoặc đã xóa.</exception>
    Task<PagedResult<CommentResponseDto>> GetCommentsAsync(
        Guid postId, Guid viewerId, int page, int size);

    /// <summary>
    /// Thêm bình luận, hoặc reply vào 1 comment gốc (chỉ hỗ trợ 1 cấp — reply vào reply bị chặn).
    /// Tạo notification cho tác giả bài (comment gốc) hoặc tác giả comment gốc (reply),
    /// bỏ qua nếu người nhận chính là userId.
    /// </summary>
    /// <param name="userId">Id tác giả comment.</param>
    /// <param name="postId">Id bài đăng.</param>
    /// <param name="dto">Nội dung, ParentCommentId (nullable).</param>
    /// <exception cref="KeyNotFoundException">404 — bài hoặc parent comment không tồn tại/đã xóa.</exception>
    /// <exception cref="ArgumentException">
    /// 400 — reply vào reply (parent đã có ParentCommentId), hoặc parent comment không thuộc post này.
    /// </exception>
    Task<CommentResponseDto> AddCommentAsync(Guid userId, Guid postId, CreateCommentDto dto);

    /// <summary>Soft-delete bình luận.</summary>
    /// <param name="userId">Id user đang thao tác.</param>
    /// <param name="commentId">Id comment cần xóa.</param>
    /// <param name="isAdmin">True nếu admin xóa (bỏ qua check ownership).</param>
    /// <exception cref="KeyNotFoundException">404 — comment không tồn tại.</exception>
    /// <exception cref="ForbiddenException">403 — không phải chủ comment và không phải admin.</exception>
    /// <exception cref="InvalidOperationException">400 — comment đã được xóa trước đó.</exception>
    Task DeleteCommentAsync(Guid userId, Guid commentId, bool isAdmin = false);

    /// <summary>
    /// Chia sẻ lại bài viết lên trang cá nhân (repost).
    /// Tạo một Post mới với OriginalPostId trỏ vào bài gốc.
    /// Không cho share bài đã là share (chặn chain).
    /// Tự động gửi notification cho tác giả bài gốc (trừ tự share bài mình).
    /// </summary>
    /// <param name="userId">Id người thực hiện share.</param>
    /// <param name="originalPostId">Id bài gốc cần share.</param>
    /// <param name="dto">Caption và privacy của bài share mới.</param>
    /// <exception cref="KeyNotFoundException">404 — bài gốc không tồn tại hoặc đã xóa.</exception>
    /// <exception cref="ForbiddenException">403 — không đủ quyền xem bài gốc.</exception>
    /// <exception cref="InvalidOperationException">400 — share bài đã là share (chain không cho phép).</exception>
    Task<PostResponseDto> SharePostAsync(Guid userId, Guid originalPostId, SharePostRequestDto dto);
}