namespace SocialApp.API.Hubs;

/// <summary>
/// Helper tập trung tên group cho SignalR hubs.
/// Dùng chung cho ChatHub và NotificationHub — đảm bảo naming nhất quán,
/// tránh hardcode string rải rác.
/// </summary>
public static class HubGroups
{
    private const string UserPrefix = "user_";
    private const string ConversationPrefix = "conv_";

    /// <summary>Group riêng của từng user — dùng để push thông báo cá nhân.</summary>
    public static string User(Guid userId) => $"{UserPrefix}{userId}";

    /// <summary>Group của một conversation — dùng để broadcast tin nhắn.</summary>
    public static string Conversation(Guid convId) => $"{ConversationPrefix}{convId}";
}