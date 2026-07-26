using Microsoft.AspNetCore.Http;

namespace SocialApp.Application.DTOs.Messages;

/// <summary>
/// DTO gửi tin nhắn qua HTTP (multipart/form-data).
/// Ít nhất một trong ba trường phải có: Content, Attachment, hoặc GifUrl.
/// </summary>
public sealed class SendMessageDto
{
    /// <summary>Id của conversation cần gửi tin nhắn vào.</summary>
    public Guid ConversationId { get; init; }

    /// <summary>
    /// Nội dung tin nhắn văn bản — nullable vì có thể gửi file không kèm text.
    /// Nếu có: tối đa 4000 ký tự, không được toàn whitespace.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// File đính kèm — nullable vì có thể gửi text thuần hoặc GIF.
    /// Validate magic bytes + ContentType + size ở service layer.
    /// Ảnh tối đa 10MB, video tối đa 50MB.
    /// </summary>
    public IFormFile? Attachment { get; init; }

    /// <summary>
    /// URL GIF từ Tenor — nullable.
    /// Khi có GifUrl, không cần Attachment.
    /// AttachmentType sẽ được set = "gif" tự động trong MessageService.
    /// </summary>
    public string? GifUrl { get; init; }
}
