using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Messages;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Interface cho toàn bộ business logic liên quan đến tin nhắn và conversation.
/// CreateOrGetConversationAsync là idempotent — conversation 1-1 đã tồn tại → trả cái cũ.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Tạo conversation mới hoặc trả về conversation đã tồn tại (idempotent với 1-1).
    /// Group conversation luôn tạo mới.
    /// </summary>
    Task<ConversationDto> CreateOrGetConversationAsync(
        Guid userId,
        CreateConversationDto dto);

    /// <summary>
    /// Lấy danh sách conversation của user, kèm LastMessage và UnreadCount.
    /// OrderBy LastMessageAt DESC.
    /// </summary>
    Task<PagedResult<ConversationDto>> GetConversationsAsync(
        Guid userId,
        int page,
        int size);

    /// <summary>
    /// Lấy danh sách tin nhắn trong conversation, OrderBy CreatedAt DESC.
    /// Kiểm tra userId có trong conversation không → 403 nếu không.
    /// Message IsDeleted = true → Content = null, AttachmentUrl = null.
    /// </summary>
    Task<PagedResult<MessageDto>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        int page,
        int size);

    /// <summary>
    /// Gửi tin nhắn qua HTTP (có thể kèm file đính kèm).
    /// Validate file: magic bytes + ContentType + size.
    /// Upload file lên R2 trước, lưu DB sau — nếu lưu DB thất bại → xóa file.
    /// </summary>
    Task<MessageDto> SendMessageAsync(
        Guid senderId,
        SendMessageDto dto);

    /// <summary>
    /// Gửi tin nhắn từ SignalR hub (không có file đính kèm).
    /// Validate content không null/whitespace.
    /// </summary>
    Task<MessageDto> SendMessageFromHubAsync(
        Guid senderId,
        SendMessageHubDto dto);

    /// <summary>
    /// Đánh dấu tất cả tin nhắn trong conversation là đã đọc.
    /// Cập nhật ConversationParticipant.LastReadAt = UtcNow.
    /// Batch insert MessageSeen cho các message chưa seen — idempotent.
    /// </summary>
    Task MarkAsSeenAsync(
        Guid userId,
        Guid conversationId);

    /// <summary>
    /// Soft delete tin nhắn. Chỉ sender mới được xóa, trong vòng 24 giờ.
    /// Nếu có file đính kèm → xóa trên R2 bất đồng bộ (fire and forget).
    /// </summary>
    Task<MessageDto> DeleteMessageAsync(
        Guid userId,
        Guid messageId);
}