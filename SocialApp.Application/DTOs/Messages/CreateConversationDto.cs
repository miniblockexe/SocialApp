namespace SocialApp.Application.DTOs.Messages;

/// <summary>
/// DTO tạo hoặc lấy conversation hiện có.
/// Với 1-1: ParticipantIds chứa đúng 1 userId (không bao gồm chính mình).
/// Với group: ParticipantIds chứa ít nhất 2 userId + GroupName bắt buộc.
/// </summary>
public sealed class CreateConversationDto
{
    /// <summary>
    /// Danh sách userId muốn tạo conversation cùng.
    /// Không bao gồm chính mình — service tự thêm userId của caller vào.
    /// </summary>
    public List<Guid> ParticipantIds { get; init; } = [];

    /// <summary>
    /// false (default) = conversation 1-1.
    /// true = group conversation.
    /// </summary>
    public bool IsGroup { get; init; } = false;

    /// <summary>
    /// Tên group — bắt buộc khi IsGroup = true, bỏ qua khi IsGroup = false.
    /// </summary>
    public string? GroupName { get; init; }
}