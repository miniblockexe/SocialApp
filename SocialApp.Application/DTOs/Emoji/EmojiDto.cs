namespace SocialApp.Application.DTOs.Emoji;

/// <summary>Emoji item từ EmojiHub API.</summary>
public sealed record EmojiDto(
    string Name,
    string Category,
    string Group,
    IReadOnlyList<string> HtmlCode,
    IReadOnlyList<string> Unicode);
