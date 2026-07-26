using Microsoft.EntityFrameworkCore;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Interface cho phép MessageService truy cập các DbSet của các entity
/// KHÔNG kế thừa BaseAuditableEntity (Conversation, ConversationParticipant,
/// Message, MessageSeen) mà KHÔNG cần reference trực tiếp tới
/// SocialApp.Infrastructure.Data.AppDbContext.
///
/// Tuân thủ Dependency Inversion: Application chỉ phụ thuộc vào interface,
/// Infrastructure implement interface này thông qua AppDbContext.
/// </summary>
public interface IMessageDbContext
{
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationParticipant> ConversationParticipants { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageSeen> MessageSeens { get; }

    /// <summary>Lưu toàn bộ thay đổi xuống DB.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}