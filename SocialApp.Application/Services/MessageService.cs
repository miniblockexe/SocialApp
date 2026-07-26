using System;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Messages;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Services;

/// <summary>
/// Implement IMessageService: tạo/lấy conversation, gửi/xóa tin nhắn,
/// lấy danh sách, mark as seen.
///
/// NOTE: Conversation, ConversationParticipant, Message, MessageSeen
/// không kế thừa BaseAuditableEntity nên không dùng IGenericRepository<T>.
/// Dùng IMessageDbContext (Application layer interface) — không reference Infrastructure.
/// User kế thừa BaseAuditableEntity → dùng IUserRepository bình thường.
/// </summary>
public sealed class MessageService : IMessageService
{
    private readonly IMessageDbContext _db;
    private readonly IUserRepository _userRepo;
    private readonly INotificationService _notificationService;
    private readonly IR2Service _r2Service;
    private readonly IMapper _mapper;
    private readonly IOptions<FileValidationSettings> _fileSettings;
    private readonly ILogger<MessageService> _logger;

    // Giới hạn kích thước file (ảnh 10MB, video/audio 50MB)
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const long MaxVideoBytes = 50 * 1024 * 1024;

    // Khoảng thời gian cho phép xóa tin nhắn
    private static readonly TimeSpan DeleteWindow = TimeSpan.FromHours(24);

    public MessageService(
        IMessageDbContext db,
        IUserRepository userRepo,
        INotificationService notificationService,
        IR2Service r2Service,
        IMapper mapper,
        IOptions<FileValidationSettings> fileSettings,
        ILogger<MessageService> logger)
    {
        _db = db;
        _userRepo = userRepo;
        _notificationService = notificationService;
        _r2Service = r2Service;
        _mapper = mapper;
        _fileSettings = fileSettings;
        _logger = logger;
    }

    public async Task<ConversationDto> CreateOrGetConversationAsync(
        Guid userId, CreateConversationDto dto)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId không hợp lệ.");

        // Loại bỏ userId khỏi participantIds nếu người dùng vô tình tự thêm mình
        var participantIds = dto.ParticipantIds
            .Where(id => id != userId)
            .Distinct()
            .ToList();

        if (!dto.IsGroup)
        {
            // Conversation 1-1
            var targetId = participantIds.FirstOrDefault();

            if (targetId == Guid.Empty)
                throw new ArgumentException("Id người nhận không hợp lệ.");

            if (targetId == userId)
                throw new ArgumentException("Không thể nhắn tin cho chính mình.");

            var target = await _userRepo.GetByIdAsync(targetId);
            if (target is null)
                throw new KeyNotFoundException("Người dùng không tồn tại.");

            // Tìm conversation 1-1 đã tồn tại (idempotent)
            var existing = await FindDirectConversationAsync(userId, targetId);
            if (existing is not null)
            {
                _logger.LogInformation(
                    "Conversation 1-1 đã tồn tại: ConvId={ConvId}, User={UserId}, Target={TargetId}",
                    existing.Id, userId, targetId);
                return await BuildConversationDtoAsync(existing, userId);
            }

            // Tạo mới
            var conversation = new Conversation
            {
                IsGroup = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();

            _db.ConversationParticipants.AddRange(
                new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                },
                new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = targetId,
                    JoinedAt = DateTime.UtcNow
                }
            );

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Tạo conversation 1-1: ConvId={ConvId}, User={UserId}, Target={TargetId}",
                conversation.Id, userId, targetId);

            return await BuildConversationDtoAsync(conversation, userId);
        }
        else
        {
            // Group conversation
            var groupName = dto.GroupName?.Trim();
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("Tên group không được để trống.");

            // Validate tất cả participant tồn tại
            foreach (var pid in participantIds)
            {
                if (pid == Guid.Empty)
                    throw new ArgumentException("Id người dùng trong group không hợp lệ.");

                var member = await _userRepo.GetByIdAsync(pid);
                if (member is null)
                    throw new KeyNotFoundException($"Người dùng {pid} không tồn tại.");
            }

            var conversation = new Conversation
            {
                IsGroup = true,
                GroupName = groupName,
                CreatedAt = DateTime.UtcNow
            };

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();

            // Thêm creator + tất cả participant
            var allMemberIds = new HashSet<Guid>(participantIds) { userId };

            _db.ConversationParticipants.AddRange(
                allMemberIds.Select(memberId => new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = memberId,
                    JoinedAt = DateTime.UtcNow
                })
            );

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Tạo group conversation: ConvId={ConvId}, Creator={UserId}, Members={Count}",
                conversation.Id, userId, allMemberIds.Count);

            return await BuildConversationDtoAsync(conversation, userId);
        }
    }

    public async Task<PagedResult<ConversationDto>> GetConversationsAsync(
        Guid userId, int page, int size)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = size < 1 ? 10 : size > 100 ? 100 : size;

        var participantQuery = _db.ConversationParticipants
            .Where(p => p.UserId == userId)
            .Include(p => p.Conversation)
            .ThenInclude(c => c.Participants)
            .ThenInclude(cp => cp.User);

        var totalCount = await participantQuery.CountAsync();

        var participantRecords = await participantQuery
            .OrderByDescending(p =>
                p.Conversation.LastMessageAt.HasValue
                    ? p.Conversation.LastMessageAt
                    : (DateTime?)p.Conversation.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync();

        var items = new List<ConversationDto>(participantRecords.Count);

        foreach (var p in participantRecords)
        {
            var conv = p.Conversation;

            // Lấy LastMessage (chưa xóa)
            var lastMessage = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.SeenBy)
                .Where(m => m.ConversationId == conv.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            // Tính UnreadCount: message sau LastReadAt, không phải của chính mình
            var lastReadAt = p.LastReadAt;
            var unreadCount = await _db.Messages.CountAsync(m =>
                m.ConversationId == conv.Id &&
                !m.IsDeleted &&
                m.SenderId != userId &&
                (lastReadAt == null || m.CreatedAt > lastReadAt));

            var participantDtos = conv.Participants
                .Select(cp => _mapper.Map<UserBriefDto>(cp.User))
                .ToList();

            items.Add(new ConversationDto
            {
                Id = conv.Id,
                IsGroup = conv.IsGroup,
                GroupName = conv.GroupName,
                GroupAvatarUrl = conv.GroupAvatarUrl,
                LastMessageAt = conv.LastMessageAt,
                LastMessage = lastMessage is null ? null : MapToMessageDto(lastMessage),
                UnreadCount = unreadCount,
                Participants = participantDtos
            });
        }

        return PagedResult<ConversationDto>.Create(items, totalCount, safePage, safeSize);
    }

    public async Task<PagedResult<MessageDto>> GetMessagesAsync(
        Guid userId, Guid conversationId, int page, int size)
    {
        if (conversationId == Guid.Empty)
            throw new ArgumentException("ConversationId không hợp lệ.");

        var safePage = page < 1 ? 1 : page;
        var safeSize = size < 1 ? 10 : size > 100 ? 100 : size;

        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isParticipant)
            throw new ForbiddenException("Bạn không có quyền xem tin nhắn trong conversation này.");

        var query = _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.SeenBy)
            .Where(m => m.ConversationId == conversationId);

        var totalCount = await query.CountAsync();

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync();

        var dtos = messages.Select(MapToMessageDto).ToList();

        return PagedResult<MessageDto>.Create(dtos, totalCount, safePage, safeSize);
    }

    public async Task<MessageDto> SendMessageAsync(Guid senderId, SendMessageDto dto)
    {
        if (senderId == Guid.Empty)
            throw new ArgumentException("SenderId không hợp lệ.");

        if (dto.ConversationId == Guid.Empty)
            throw new ArgumentException("ConversationId không hợp lệ.");

        var participant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p =>
                p.ConversationId == dto.ConversationId &&
                p.UserId == senderId);

        if (participant is null)
            throw new ForbiddenException("Bạn không có quyền gửi tin nhắn trong conversation này.");

        var trimmedContent = dto.Content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedContent) && dto.Attachment is null && string.IsNullOrWhiteSpace(dto.GifUrl))
            throw new ArgumentException("Tin nhắn phải có nội dung, file đính kèm, hoặc GIF.");

        string? attachmentUrl = null;
        string? attachmentType = null;
        string? r2ObjectKey = null;

        if (dto.Attachment is not null)
        {
            var file = dto.Attachment;

            if (file.Length == 0)
                throw new ArgumentException("File đính kèm không được rỗng.");

            var settings = _fileSettings.Value;
            var contentType = file.ContentType.ToLowerInvariant();

            bool isImage = settings.AllowedImageContentTypes
                .Select(t => t.ToLowerInvariant()).Contains(contentType);
            bool isVideo = settings.AllowedVideoContentTypes
                .Select(t => t.ToLowerInvariant()).Contains(contentType);
            bool isAudio = settings.AllowedAudioContentTypes
                .Select(t => t.ToLowerInvariant()).Contains(contentType);

            if (!isImage && !isVideo && !isAudio)
                throw new ArgumentException($"Loại file không được hỗ trợ: {contentType}.");

            if (isImage && file.Length > MaxImageBytes)
                throw new ArgumentException("Ảnh không được vượt quá 10MB.");

            if ((isVideo || isAudio) && file.Length > MaxVideoBytes)
                throw new ArgumentException("Video/audio không được vượt quá 50MB.");

            // Validate magic bytes cho ảnh
            if (isImage && settings.ImageMagicBytes.TryGetValue(contentType, out var magicHex))
            {
                using var stream = file.OpenReadStream();
                var headerBytes = new byte[magicHex.Length / 2];
                await stream.ReadAsync(headerBytes);
                var fileHex = Convert.ToHexString(headerBytes);

                if (!fileHex.StartsWith(magicHex, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("File không hợp lệ — magic bytes không khớp.");
            }

            attachmentType = isImage ? "image" : isVideo ? "video" : "audio";
            r2ObjectKey = $"messages/{dto.ConversationId}/{Guid.NewGuid()}_{file.FileName}";

            try
            {
                var uploadResult = await _r2Service.UploadAsync(file, $"messages/{dto.ConversationId}");
                attachmentUrl = uploadResult.SecureUrl;
                r2ObjectKey = uploadResult.PublicId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Upload file đính kèm thất bại. ConvId={ConvId}, Sender={SenderId}",
                    dto.ConversationId, senderId);
                throw new InvalidOperationException("Không thể upload file đính kèm. Vui lòng thử lại.");
            }
        }

        // GIF từ Tenor: dùng GifUrl trực tiếp, không upload file
        if (!string.IsNullOrWhiteSpace(dto.GifUrl) && dto.Attachment is null)
        {
            attachmentUrl  = dto.GifUrl.Trim();
            attachmentType = "gif";
        }

        var message = new Message
        {
            ConversationId = dto.ConversationId,
            SenderId = senderId,
            Content = string.IsNullOrWhiteSpace(trimmedContent) ? null : trimmedContent,
            AttachmentUrl = attachmentUrl,
            AttachmentType = attachmentType,
            IsAI = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _db.Messages.Add(message);

            // Cập nhật LastMessageAt trên Conversation
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == dto.ConversationId);
            if (conversation is not null)
                conversation.LastMessageAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Lưu DB thất bại → xóa file đã upload (rollback cloud)
            if (r2ObjectKey is not null)
            {
                _ = Task.Run(async () =>
                {
                    try { await _r2Service.DeleteAsync(r2ObjectKey); }
                    catch (Exception delEx)
                    {
                        _logger.LogWarning(delEx,
                            "Xóa file R2 sau lỗi DB thất bại. Key={Key}", r2ObjectKey);
                    }
                });
            }

            _logger.LogError(ex,
                "Lưu message thất bại. ConvId={ConvId}, Sender={SenderId}",
                dto.ConversationId, senderId);
            throw;
        }

        // Tự động seen cho sender
        _db.MessageSeens.Add(new MessageSeen
        {
            MessageId = message.Id,
            UserId = senderId,
            SeenAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Load sender cho DTO
        var sender = await _userRepo.GetByIdAsync(senderId);
        message.Sender = sender!;
        message.SeenBy = [new MessageSeen { MessageId = message.Id, UserId = senderId }];

        // Tạo notification cho tất cả participant khác
        var otherParticipants = await _db.ConversationParticipants
            .Where(p => p.ConversationId == dto.ConversationId && p.UserId != senderId)
            .ToListAsync();

        foreach (var p in otherParticipants)
        {
            await _notificationService.CreateNotificationAsync(
                recipientId: p.UserId,
                actorId: senderId,
                type: NotificationType.Message,
                entityId: message.Id,
                content: $"{sender?.FullName ?? "Ai đó"} đã gửi tin nhắn cho bạn.");
        }

        _logger.LogInformation(
            "Message sent: MessageId={MessageId}, ConvId={ConvId}, Sender={SenderId}",
            message.Id, dto.ConversationId, senderId);

        return MapToMessageDto(message);
    }

    public async Task<MessageDto> SendMessageFromHubAsync(Guid senderId, SendMessageHubDto dto)
    {
        if (senderId == Guid.Empty)
            throw new ArgumentException("SenderId không hợp lệ.");

        if (dto.ConversationId == Guid.Empty)
            throw new ArgumentException("ConversationId không hợp lệ.");

        var trimmedContent = dto.Content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedContent))
            throw new ArgumentException("Nội dung tin nhắn không được để trống.");

        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == dto.ConversationId && p.UserId == senderId);

        if (!isParticipant)
            throw new ForbiddenException("Bạn không có quyền gửi tin nhắn trong conversation này.");

        var message = new Message
        {
            ConversationId = dto.ConversationId,
            SenderId = senderId,
            Content = trimmedContent,
            IsAI = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Messages.Add(message);

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == dto.ConversationId);
        if (conversation is not null)
            conversation.LastMessageAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Tự động seen cho sender
        _db.MessageSeens.Add(new MessageSeen
        {
            MessageId = message.Id,
            UserId = senderId,
            SeenAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var sender = await _userRepo.GetByIdAsync(senderId);
        message.Sender = sender!;
        message.SeenBy = [new MessageSeen { MessageId = message.Id, UserId = senderId }];

        // Notification cho các participant khác
        var otherParticipants = await _db.ConversationParticipants
            .Where(p => p.ConversationId == dto.ConversationId && p.UserId != senderId)
            .ToListAsync();

        foreach (var p in otherParticipants)
        {
            await _notificationService.CreateNotificationAsync(
                recipientId: p.UserId,
                actorId: senderId,
                type: NotificationType.Message,
                entityId: message.Id,
                content: $"{sender?.FullName ?? "Ai đó"} đã gửi tin nhắn cho bạn.");
        }

        _logger.LogInformation(
            "Hub message sent: MessageId={MessageId}, ConvId={ConvId}, Sender={SenderId}",
            message.Id, dto.ConversationId, senderId);

        return MapToMessageDto(message);
    }

    public async Task MarkAsSeenAsync(Guid userId, Guid conversationId)
    {
        if (userId == Guid.Empty || conversationId == Guid.Empty) return;

        var participant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p =>
                p.ConversationId == conversationId && p.UserId == userId);

        if (participant is null)
            throw new ForbiddenException("Bạn không có quyền trong conversation này.");

        participant.LastReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Lấy các messageId chưa có seen của userId trong conversation này
        var allMessageIds = await _db.Messages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .Select(m => m.Id)
            .ToListAsync();

        if (allMessageIds.Count == 0) return;

        var alreadySeenIds = await _db.MessageSeens
            .Where(ms => ms.UserId == userId && allMessageIds.Contains(ms.MessageId))
            .Select(ms => ms.MessageId)
            .ToListAsync();

        var unseenIds = allMessageIds.Except(alreadySeenIds).ToList();

        if (unseenIds.Count == 0) return;

        // Batch insert MessageSeen (idempotent)
        _db.MessageSeens.AddRange(
            unseenIds.Select(messageId => new MessageSeen
            {
                MessageId = messageId,
                UserId = userId,
                SeenAt = DateTime.UtcNow
            })
        );

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "MarkAsSeen: UserId={UserId}, ConvId={ConvId}, Marked={Count} messages",
            userId, conversationId, unseenIds.Count);
    }

    public async Task<MessageDto> DeleteMessageAsync(Guid userId, Guid messageId)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("MessageId không hợp lệ.");

        var message = await _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.SeenBy)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message is null)
            throw new KeyNotFoundException("Tin nhắn không tồn tại.");

        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == message.ConversationId && p.UserId == userId);

        if (!isParticipant)
            throw new ForbiddenException("Bạn không có quyền trong conversation này.");

        if (message.SenderId != userId)
            throw new ForbiddenException("Chỉ có thể xóa tin nhắn của mình.");

        if (message.IsDeleted)
            throw new InvalidOperationException("Tin nhắn đã được xóa.");

        if (message.CreatedAt < DateTime.UtcNow.Subtract(DeleteWindow))
            throw new InvalidOperationException("Chỉ có thể xóa tin nhắn trong vòng 24 giờ.");

        var attachmentUrl = message.AttachmentUrl;

        // Soft delete
        message.IsDeleted = true;
        message.Content = null;
        message.AttachmentUrl = null;
        await _db.SaveChangesAsync();

        // Xóa file R2 bất đồng bộ (fire and forget)
        if (!string.IsNullOrWhiteSpace(attachmentUrl))
        {
            _ = Task.Run(async () =>
            {
                try { await _r2Service.DeleteAsync(attachmentUrl); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Xóa file R2 sau delete message thất bại. Url={Url}", attachmentUrl);
                }
            });
        }

        _logger.LogInformation(
            "Message deleted: MessageId={MessageId}, DeletedBy={UserId}",
            messageId, userId);

        return MapToMessageDto(message);
    }

    /// <summary>Tìm conversation 1-1 giữa 2 user (cả 2 chiều).</summary>
    private async Task<Conversation?> FindDirectConversationAsync(Guid userA, Guid userB)
    {
        var userAConvIds = await _db.ConversationParticipants
            .Where(p => p.UserId == userA)
            .Select(p => p.ConversationId)
            .ToListAsync();

        var userBConvIds = await _db.ConversationParticipants
            .Where(p => p.UserId == userB)
            .Select(p => p.ConversationId)
            .ToListAsync();

        var commonIds = userAConvIds.Intersect(userBConvIds).ToList();
        if (commonIds.Count == 0) return null;

        foreach (var convId in commonIds)
        {
            var conv = await _db.Conversations
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == convId && !c.IsGroup);

            if (conv is not null && conv.Participants.Count == 2)
                return conv;
        }

        return null;
    }

    /// <summary>Build ConversationDto đầy đủ từ Conversation entity.</summary>
    private async Task<ConversationDto> BuildConversationDtoAsync(
        Conversation conv, Guid userId)
    {
        List<ConversationParticipant> participants;
        if (conv.Participants.Count == 0)
        {
            participants = await _db.ConversationParticipants
                .Include(p => p.User)
                .Where(p => p.ConversationId == conv.Id)
                .ToListAsync();
        }
        else
        {
            participants = conv.Participants.ToList();
        }

        var lastMessage = await _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.SeenBy)
            .Where(m => m.ConversationId == conv.Id && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        var myParticipant = participants.FirstOrDefault(p => p.UserId == userId);
        var lastReadAt = myParticipant?.LastReadAt;

        var unreadCount = await _db.Messages.CountAsync(m =>
            m.ConversationId == conv.Id &&
            !m.IsDeleted &&
            m.SenderId != userId &&
            (lastReadAt == null || m.CreatedAt > lastReadAt));

        return new ConversationDto
        {
            Id = conv.Id,
            IsGroup = conv.IsGroup,
            GroupName = conv.GroupName,
            GroupAvatarUrl = conv.GroupAvatarUrl,
            LastMessageAt = conv.LastMessageAt,
            LastMessage = lastMessage is null ? null : MapToMessageDto(lastMessage),
            UnreadCount = unreadCount,
            Participants = participants
                .Select(p => _mapper.Map<UserBriefDto>(p.User))
                .ToList()
        };
    }

    /// <summary>Map Message entity → MessageDto, ẩn content/attachment nếu IsDeleted.</summary>
    private static MessageDto MapToMessageDto(Message m)
    {
        return new MessageDto
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            Content = m.IsDeleted ? null : m.Content,
            IsAI = m.IsAI,
            AttachmentUrl = m.IsDeleted ? null : m.AttachmentUrl,
            AttachmentType = m.IsDeleted ? null : m.AttachmentType,
            CreatedAt = m.CreatedAt,
            IsDeleted = m.IsDeleted,
            Sender = new UserBriefDto
            {
                Id = m.Sender.Id,
                Username = m.Sender.Username,
                FullName = m.Sender.FullName,
                AvatarUrl = m.Sender.AvatarUrl,
                Role = m.Sender.Role
            },
            SeenByUserIds = m.SeenBy.Select(s => s.UserId).ToList()
        };
    }
}