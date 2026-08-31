using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Common;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Infrastructure.Data;

public class AppDbContext : DbContext, IMessageDbContext, IAdminDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMediaFile> PostMediaFiles => Set<PostMediaFile>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageSeen> MessageSeens => Set<MessageSeen>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupPost> GroupPosts => Set<GroupPost>();
    public DbSet<GroupJoinRequest> GroupJoinRequests => Set<GroupJoinRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter — soft-delete
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseAuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression
                    .Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression
                    .Property(parameter, nameof(BaseAuditableEntity.DeletedAt));
                var condition = System.Linq.Expressions.Expression
                    .Equal(property, System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?)));
                var lambda = System.Linq.Expressions.Expression
                    .Lambda(condition, parameter);

                entityType.SetQueryFilter(lambda);
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<FriendRequest>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Property(e => e.CreatedAt).IsModified = false;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

// ── Entity Configurations ─────────────────────────────────────────────────────

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).IsRequired().HasMaxLength(6);
        builder.Property(t => t.IsUsed).HasDefaultValue(false);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.UserId, t.Token })
            .HasDatabaseName("IX_PasswordResetTokens_UserId_Token");
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).IsRequired().HasMaxLength(50).UseCollation("case_insensitive");
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256).UseCollation("case_insensitive");
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Bio).HasMaxLength(500);
        builder.Property(u => u.AvatarUrl).HasMaxLength(1024);
        builder.Property(u => u.AvatarPublicId).HasMaxLength(512);
        builder.Property(u => u.CoverPhotoUrl).HasMaxLength(1024);
        builder.Property(u => u.CoverPublicId).HasMaxLength(512);
        builder.Property(u => u.BannedReason).HasMaxLength(1000);
        builder.Property(u => u.Role).HasConversion<int>().HasDefaultValue(UserRole.User);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.IsBanned).HasDefaultValue(false);

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_Users_Email");
        builder.HasIndex(u => u.Username).IsUnique().HasDatabaseName("IX_Users_Username");
        builder.HasIndex(u => u.LastSeen).HasDatabaseName("IX_Users_LastSeen");
        builder.HasIndex(u => u.CreatedAt).HasDatabaseName("IX_Users_CreatedAt");

        builder.HasMany(u => u.Posts).WithOne(p => p.User).HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(u => u.SentMessages).WithOne(m => m.Sender).HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(u => u.Notifications).WithOne(n => n.User).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(u => u.RefreshTokens).WithOne(rt => rt.User).HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasData(new User
        {
            Id = new Guid("00000000-0000-0000-0000-000000000001"),
            Username = "admin",
            Email = "admin@socialapp.com",
            PasswordHash = "$2b$12$34GAiG2UMvVZoOAESavdQuCWoRQ7NEUEnE3U1/M8uzRpO7k6S3Oq6",
            FullName = "System Administrator",
            Role = UserRole.Admin,
            IsActive = true,
            IsBanned = false,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        builder.HasData(new User
        {
            Id = new Guid("00000000-0000-0000-0000-000000000002"),
            Username = "ai_assistant",
            Email = "ai@socialapp.com",
            PasswordHash = "$2b$12$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            FullName = "SocialApp AI",
            Role = UserRole.User,
            IsActive = true,
            IsBanned = false,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Token).IsRequired().HasMaxLength(512);
        builder.Property(rt => rt.IsRevoked).HasDefaultValue(false);
        builder.Ignore(rt => rt.IsActive);

        builder.HasIndex(rt => rt.Token).IsUnique().HasDatabaseName("IX_RefreshTokens_Token");
        builder.HasIndex(rt => new { rt.UserId, rt.IsRevoked }).HasDatabaseName("IX_RefreshTokens_UserId_IsRevoked");
    }
}

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Content).HasMaxLength(5000);
        builder.Property(p => p.Privacy).HasConversion<int>().HasDefaultValue(PostPrivacy.Public);

        builder.HasIndex(p => new { p.UserId, p.CreatedAt }).HasDatabaseName("IX_Posts_UserId_CreatedAt").IsDescending(false, true);
        builder.HasIndex(p => p.DeletedAt).HasDatabaseName("IX_Posts_DeletedAt");

        builder.HasMany(p => p.PostMediaFiles).WithOne(pmf => pmf.Post).HasForeignKey(pmf => pmf.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Comments).WithOne(c => c.Post).HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Likes).WithOne(l => l.Post).HasForeignKey(l => l.PostId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.OriginalPostId).IsRequired(false);
        builder.HasOne(p => p.OriginalPost)
               .WithMany(p => p.Shares)
               .HasForeignKey(p => p.OriginalPostId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(p => p.OriginalPostId).HasDatabaseName("IX_Posts_OriginalPostId");

        builder.Property(p => p.GroupId).IsRequired(false);
        builder.HasIndex(p => p.GroupId).HasDatabaseName("IX_Posts_GroupId");
        builder.HasOne(p => p.Group)
               .WithMany()
               .HasForeignKey(p => p.GroupId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PostMediaFileConfiguration : IEntityTypeConfiguration<PostMediaFile>
{
    public void Configure(EntityTypeBuilder<PostMediaFile> builder)
    {
        builder.ToTable("PostMediaFiles");
        builder.HasKey(pmf => pmf.Id);

        builder.Property(pmf => pmf.MediaUrl).IsRequired().HasMaxLength(1024);
        builder.Property(pmf => pmf.PublicId).IsRequired().HasMaxLength(512);
        builder.Property(pmf => pmf.MediaType).HasConversion<int>();
        builder.Property(pmf => pmf.StorageProvider).HasConversion<int>();

        builder.HasIndex(pmf => pmf.PostId).HasDatabaseName("IX_PostMediaFiles_PostId");
    }
}

internal sealed class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> builder)
    {
        builder.ToTable("Likes");
        builder.HasKey(l => l.Id);

        builder.HasIndex(l => new { l.UserId, l.PostId }).IsUnique().HasDatabaseName("IX_Likes_UserId_PostId");

        builder.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Post).WithMany(p => p.Likes).HasForeignKey(l => l.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content).IsRequired().HasMaxLength(2000);

        builder.HasIndex(c => new { c.PostId, c.CreatedAt }).HasDatabaseName("IX_Comments_PostId_CreatedAt");
        builder.HasIndex(c => c.ParentCommentId).HasDatabaseName("IX_Comments_ParentCommentId");

        builder.HasOne(c => c.ParentComment).WithMany(c => c.Replies).HasForeignKey(c => c.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> builder)
    {
        builder.ToTable("FriendRequests");
        builder.HasKey(fr => fr.Id);

        builder.Property(fr => fr.Status).HasConversion<int>().HasDefaultValue(FriendStatus.Pending);

        builder.HasIndex(fr => new { fr.SenderId, fr.ReceiverId }).IsUnique().HasDatabaseName("IX_FriendRequests_SenderId_ReceiverId");

        builder.HasOne(fr => fr.Sender).WithMany(u => u.SentFriendRequests).HasForeignKey(fr => fr.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(fr => fr.Receiver).WithMany(u => u.ReceivedFriendRequests).HasForeignKey(fr => fr.ReceiverId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.GroupName).HasMaxLength(100);
        builder.Property(c => c.GroupAvatarUrl).HasMaxLength(1024);
        builder.Property(c => c.IsGroup).HasDefaultValue(false);

        builder.HasIndex(c => c.LastMessageAt).HasDatabaseName("IX_Conversations_LastMessageAt");
    }
}

internal sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("ConversationParticipants");
        builder.HasKey(cp => new { cp.ConversationId, cp.UserId });

        builder.HasIndex(cp => cp.UserId).HasDatabaseName("IX_ConversationParticipants_UserId");

        builder.HasOne(cp => cp.Conversation).WithMany(c => c.Participants).HasForeignKey(cp => cp.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(cp => cp.User).WithMany(u => u.Conversations).HasForeignKey(cp => cp.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content).HasMaxLength(4000);
        builder.Property(m => m.AttachmentUrl).HasMaxLength(1024);
        builder.Property(m => m.AttachmentType).HasMaxLength(100);
        builder.Property(m => m.IsAI).HasDefaultValue(false);
        builder.Property(m => m.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt }).HasDatabaseName("IX_Messages_ConversationId_CreatedAt");

        builder.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.Sender).WithMany(u => u.SentMessages).HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(m => m.SharedPostId).IsRequired(false);

        builder.HasOne(m => m.SharedPost)
               .WithMany()
               .HasForeignKey(m => m.SharedPostId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.SharedPostId).HasDatabaseName("IX_Messages_SharedPostId");
    }
}

internal sealed class MessageSeenConfiguration : IEntityTypeConfiguration<MessageSeen>
{
    public void Configure(EntityTypeBuilder<MessageSeen> builder)
    {
        builder.ToTable("MessageSeens");
        builder.HasKey(ms => new { ms.MessageId, ms.UserId });

        builder.HasOne(ms => ms.Message).WithMany(m => m.SeenBy).HasForeignKey(ms => ms.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ms => ms.User).WithMany().HasForeignKey(ms => ms.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).HasConversion<int>();
        builder.Property(n => n.Content).IsRequired().HasMaxLength(500);
        builder.Property(n => n.IsRead).HasDefaultValue(false);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .HasDatabaseName("IX_Notifications_UserId_IsRead_CreatedAt")
            .IsDescending(false, false, true);

        builder.HasOne(n => n.User).WithMany(u => u.Notifications).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.Actor).WithMany().HasForeignKey(n => n.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);
        builder.Property(g => g.Description).HasMaxLength(1000);
        builder.Property(g => g.AvatarUrl).HasMaxLength(1024);
        builder.Property(g => g.AvatarPublicId).HasMaxLength(512);
        builder.Property(g => g.CoverUrl).HasMaxLength(1024);
        builder.Property(g => g.CoverPublicId).HasMaxLength(512);
        builder.Property(g => g.Privacy).HasConversion<int>().HasDefaultValue(GroupPrivacy.Public);
        builder.Property(g => g.RequireApproval).HasDefaultValue(false);
        builder.Property(g => g.RequirePostApproval).HasDefaultValue(false);

        builder.HasIndex(g => g.Name).HasDatabaseName("IX_Groups_Name");
        builder.HasIndex(g => g.OwnerId).HasDatabaseName("IX_Groups_OwnerId");

        builder.HasOne(g => g.Owner)
               .WithMany()
               .HasForeignKey(g => g.OwnerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Members)
               .WithOne(m => m.Group)
               .HasForeignKey(m => m.GroupId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.JoinRequests)
               .WithOne(r => r.Group)
               .HasForeignKey(r => r.GroupId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("GroupMembers");
        builder.HasKey(m => new { m.GroupId, m.UserId });
        builder.Property(m => m.Role).HasConversion<int>().HasDefaultValue(GroupRole.Member);
        builder.Property(m => m.JoinedAt).IsRequired();

        builder.HasIndex(m => m.UserId).HasDatabaseName("IX_GroupMembers_UserId");
        builder.HasIndex(m => new { m.GroupId, m.Role }).HasDatabaseName("IX_GroupMembers_GroupId_Role");

        builder.HasOne(m => m.User)
               .WithMany()
               .HasForeignKey(m => m.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GroupPostConfiguration : IEntityTypeConfiguration<GroupPost>
{
    public void Configure(EntityTypeBuilder<GroupPost> builder)
    {
        builder.ToTable("GroupPosts");
        builder.HasKey(gp => new { gp.PostId, gp.GroupId });
        builder.Property(gp => gp.Status).HasConversion<int>().HasDefaultValue(GroupPostStatus.Approved);

        builder.HasIndex(gp => new { gp.GroupId, gp.Status }).HasDatabaseName("IX_GroupPosts_GroupId_Status");

        builder.HasOne(gp => gp.Post)
               .WithOne(p => p.GroupPost)
               .HasForeignKey<GroupPost>(gp => gp.PostId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gp => gp.Group)
               .WithMany(g => g.GroupPosts)
               .HasForeignKey(gp => gp.GroupId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gp => gp.ReviewedBy)
               .WithMany()
               .HasForeignKey(gp => gp.ReviewedByUserId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class GroupJoinRequestConfiguration : IEntityTypeConfiguration<GroupJoinRequest>
{
    public void Configure(EntityTypeBuilder<GroupJoinRequest> builder)
    {
        builder.ToTable("GroupJoinRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<int>().HasDefaultValue(JoinRequestStatus.Pending);
        builder.Property(r => r.RejectReason).HasMaxLength(500);

        builder.HasIndex(r => new { r.GroupId, r.UserId, r.Status })
               .HasDatabaseName("IX_GroupJoinRequests_GroupId_UserId_Status");

        builder.HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReviewedBy)
               .WithMany()
               .HasForeignKey(r => r.ReviewedByUserId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
    }
}