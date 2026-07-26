using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Friends;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Services;

/// <summary>
/// Implement IFriendService: gửi/chấp nhận/từ chối lời mời, hủy kết bạn,
/// block/unblock, gợi ý bạn bè, lấy danh sách, kiểm tra trạng thái quan hệ.
/// </summary>
public sealed class FriendService : IFriendService
{
    // None = 99 — dùng nội bộ để phân biệt "không có record" với các status thực
    private const int FriendStatusNone = 99;

    private readonly IFriendRequestRepository _friendRepo;
    private readonly IUserRepository _userRepo;
    private readonly INotificationService _notificationService;
    private readonly IMapper _mapper;
    private readonly ILogger<FriendService> _logger;

    public FriendService(
        IFriendRequestRepository friendRepo,
        IUserRepository userRepo,
        INotificationService notificationService,
        IMapper mapper,
        ILogger<FriendService> logger)
    {
        _friendRepo = friendRepo;
        _userRepo = userRepo;
        _notificationService = notificationService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<FriendResponseDto> SendRequestAsync(Guid senderId, Guid receiverId)
    {
        // Defensive: reject Guid.Empty (validator đã check receiverId, nhưng double-check senderId)
        if (senderId == Guid.Empty || receiverId == Guid.Empty)
            throw new ArgumentException("Id người dùng không hợp lệ.");

        // Không được gửi cho chính mình
        if (senderId == receiverId)
            throw new ArgumentException("Không thể gửi lời mời cho chính mình.");

        // Kiểm tra receiver tồn tại
        var receiver = await _userRepo.GetByIdAsync(receiverId);
        if (receiver is null)
            throw new KeyNotFoundException("Người dùng không tồn tại.");

        // Kiểm tra sender có bị receiver block không
        // → tìm FriendRequest (SenderId=receiver, ReceiverId=sender, Status=Blocked)
        var blockedBySender = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == receiverId &&
                fr.ReceiverId == senderId &&
                fr.Status == FriendStatus.Blocked);

        if (blockedBySender is not null)
        {
            // 404 — không lộ user tồn tại với người bị block
            throw new KeyNotFoundException("Người dùng không tồn tại.");
        }

        // Kiểm tra receiver có bị sender block không
        // → tìm FriendRequest (SenderId=sender, ReceiverId=receiver, Status=Blocked)
        var blockedByReceiver = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == senderId &&
                fr.ReceiverId == receiverId &&
                fr.Status == FriendStatus.Blocked);

        if (blockedByReceiver is not null)
            throw new ArgumentException("Bạn đã chặn người này, hãy bỏ chặn trước.");

        // Kiểm tra đã là bạn chưa (cả 2 chiều, status = Accepted)
        var existingFriendship = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                fr.Status == FriendStatus.Accepted &&
                ((fr.SenderId == senderId && fr.ReceiverId == receiverId) ||
                 (fr.SenderId == receiverId && fr.ReceiverId == senderId)));

        if (existingFriendship is not null)
            throw new ArgumentException("Hai người đã là bạn bè.");

        // CROSS REQUEST: receiver đã gửi request cho sender chưa?
        var crossRequest = await _friendRepo.Query()
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == receiverId &&
                fr.ReceiverId == senderId &&
                fr.Status == FriendStatus.Pending);

        if (crossRequest is not null)
        {
            // Auto accept — B đã gửi cho A, A bây giờ gửi cho B → chấp nhận luôn
            crossRequest.Status = FriendStatus.Accepted;
            crossRequest.UpdatedAt = DateTime.UtcNow;
            _friendRepo.Update(crossRequest);
            await _friendRepo.SaveChangesAsync();

            _logger.LogInformation(
                "Cross request auto-accepted: RequestId={RequestId}, Sender={Sender}, Receiver={Receiver}",
                crossRequest.Id, receiverId, senderId);

            // Tạo notification FriendAccepted cho cả 2
            await _notificationService.CreateNotificationAsync(
                recipientId: senderId,
                actorId: receiverId,
                type: NotificationType.FriendAccepted,
                entityId: crossRequest.Id,
                content: $"{crossRequest.Sender.FullName} đã chấp nhận lời mời kết bạn.");

            await _notificationService.CreateNotificationAsync(
                recipientId: receiverId,
                actorId: senderId,
                type: NotificationType.FriendAccepted,
                entityId: crossRequest.Id,
                content: $"{crossRequest.Receiver.FullName} đã chấp nhận lời mời kết bạn.");

            return MapToFriendResponseDto(crossRequest);
        }

        // Kiểm tra sender đã gửi request pending rồi chưa
        var existingPending = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == senderId &&
                fr.ReceiverId == receiverId &&
                fr.Status == FriendStatus.Pending);

        if (existingPending is not null)
            throw new ArgumentException("Đã gửi lời mời kết bạn, vui lòng chờ xác nhận.");

        // Kiểm tra request bị reject trước đó → reuse record thay vì tạo mới
        var rejectedRequest = await _friendRepo.Query()
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == senderId &&
                fr.ReceiverId == receiverId &&
                fr.Status == FriendStatus.Rejected);

        FriendRequest friendRequest;

        if (rejectedRequest is not null)
        {
            // Reuse: update lại thành Pending
            rejectedRequest.Status = FriendStatus.Pending;
            rejectedRequest.UpdatedAt = DateTime.UtcNow;
            _friendRepo.Update(rejectedRequest);
            await _friendRepo.SaveChangesAsync();
            friendRequest = rejectedRequest;
        }
        else
        {
            // Tạo mới
            var sender = await _userRepo.GetByIdAsync(senderId);
            if (sender is null)
                throw new KeyNotFoundException("Người dùng không tồn tại.");

            friendRequest = new FriendRequest
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Status = FriendStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _friendRepo.AddAsync(friendRequest);
            await _friendRepo.SaveChangesAsync();

            // Reload navigation properties
            friendRequest.Sender = sender;
            friendRequest.Receiver = receiver;
        }

        // Tạo notification FriendRequest cho receiver
        await _notificationService.CreateNotificationAsync(
            recipientId: receiverId,
            actorId: senderId,
            type: NotificationType.FriendRequest,
            entityId: friendRequest.Id,
            content: $"{friendRequest.Sender.FullName} đã gửi lời mời kết bạn.");

        _logger.LogInformation(
            "Friend request sent: RequestId={RequestId}, Sender={Sender}, Receiver={Receiver}",
            friendRequest.Id, senderId, receiverId);

        return MapToFriendResponseDto(friendRequest);
    }

    public async Task<FriendResponseDto> AcceptRequestAsync(Guid userId, Guid requestId)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId không hợp lệ.");

        var request = await _friendRepo.Query()
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .FirstOrDefaultAsync(fr => fr.Id == requestId);

        if (request is null)
            throw new KeyNotFoundException("Lời mời kết bạn không tồn tại.");

        if (request.ReceiverId != userId)
            throw new ForbiddenException("Bạn không có quyền xác nhận lời mời này.");

        if (request.Status != FriendStatus.Pending)
            throw new InvalidOperationException("Lời mời không còn hiệu lực.");

        request.Status = FriendStatus.Accepted;
        request.UpdatedAt = DateTime.UtcNow;
        _friendRepo.Update(request);
        await _friendRepo.SaveChangesAsync();

        // Tạo notification FriendAccepted cho sender
        await _notificationService.CreateNotificationAsync(
            recipientId: request.SenderId,
            actorId: userId,
            type: NotificationType.FriendAccepted,
            entityId: request.Id,
            content: $"{request.Receiver.FullName} đã chấp nhận lời mời kết bạn.");

        _logger.LogInformation(
            "Friend request accepted: RequestId={RequestId}, AcceptedBy={UserId}",
            requestId, userId);

        return MapToFriendResponseDto(request);
    }

    public async Task<FriendResponseDto> RejectRequestAsync(Guid userId, Guid requestId)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId không hợp lệ.");

        var request = await _friendRepo.Query()
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .FirstOrDefaultAsync(fr => fr.Id == requestId);

        if (request is null)
            throw new KeyNotFoundException("Lời mời kết bạn không tồn tại.");

        if (request.ReceiverId != userId)
            throw new ForbiddenException("Bạn không có quyền từ chối lời mời này.");

        if (request.Status != FriendStatus.Pending)
            throw new InvalidOperationException("Lời mời không còn hiệu lực.");

        request.Status = FriendStatus.Rejected;
        request.UpdatedAt = DateTime.UtcNow;
        _friendRepo.Update(request);
        await _friendRepo.SaveChangesAsync();

        // KHÔNG tạo notification — tránh lộ thông tin người gửi bị từ chối

        _logger.LogInformation(
            "Friend request rejected: RequestId={RequestId}, RejectedBy={UserId}",
            requestId, userId);

        return MapToFriendResponseDto(request);
    }

    public async Task UnfriendAsync(Guid userId, Guid targetId)
    {
        if (userId == Guid.Empty || targetId == Guid.Empty)
            throw new ArgumentException("Id người dùng không hợp lệ.");

        if (userId == targetId)
            throw new ArgumentException("Id người dùng không hợp lệ.");

        // Tìm friendship cả 2 chiều, status = Accepted
        var friendship = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                fr.Status == FriendStatus.Accepted &&
                ((fr.SenderId == userId && fr.ReceiverId == targetId) ||
                 (fr.SenderId == targetId && fr.ReceiverId == userId)));

        if (friendship is null)
            throw new KeyNotFoundException("Hai người không phải bạn bè.");

        // Hard delete — để có thể gửi lại request sau
        _friendRepo.Remove(friendship);
        await _friendRepo.SaveChangesAsync();

        // KHÔNG tạo notification

        _logger.LogInformation(
            "Unfriend: UserId={UserId}, TargetId={TargetId}, RecordId={RecordId}",
            userId, targetId, friendship.Id);
    }

    public async Task BlockUserAsync(Guid userId, Guid targetId)
    {
        if (userId == Guid.Empty || targetId == Guid.Empty)
            throw new ArgumentException("Id người dùng không hợp lệ.");

        if (userId == targetId)
            throw new ArgumentException("Không thể chặn chính mình.");

        var target = await _userRepo.GetByIdAsync(targetId);
        if (target is null)
            throw new KeyNotFoundException("Người dùng không tồn tại.");

        // Nếu đang là bạn bè hoặc có pending request (bất kỳ chiều) → xóa trước
        var existingRelation = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                (fr.Status == FriendStatus.Accepted || fr.Status == FriendStatus.Pending) &&
                ((fr.SenderId == userId && fr.ReceiverId == targetId) ||
                 (fr.SenderId == targetId && fr.ReceiverId == userId)));

        if (existingRelation is not null)
        {
            _friendRepo.Remove(existingRelation);
            // Không SaveChanges ngay — gộp vào 1 transaction bên dưới
        }

        // Kiểm tra đã block rồi chưa
        var existingBlock = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == userId &&
                fr.ReceiverId == targetId &&
                fr.Status == FriendStatus.Blocked);

        if (existingBlock is not null)
        {
            // Rollback remove nếu có (vì dùng EF tracking, reset về detach)
            // Thực ra nếu đã block thì existingRelation không thể là Accepted/Pending
            // nhưng guard này đảm bảo an toàn
            throw new ArgumentException("Đã chặn người này rồi.");
        }

        // Tạo block record
        var blockRecord = new FriendRequest
        {
            SenderId = userId,
            ReceiverId = targetId,
            Status = FriendStatus.Blocked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _friendRepo.AddAsync(blockRecord);
        await _friendRepo.SaveChangesAsync();

        // KHÔNG tạo notification

        _logger.LogInformation(
            "User blocked: BlockerId={UserId}, BlockedId={TargetId}",
            userId, targetId);
    }

    public async Task UnblockUserAsync(Guid userId, Guid targetId)
    {
        if (userId == Guid.Empty || targetId == Guid.Empty)
            throw new ArgumentException("Id người dùng không hợp lệ.");

        var blockRecord = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == userId &&
                fr.ReceiverId == targetId &&
                fr.Status == FriendStatus.Blocked);

        if (blockRecord is null)
            throw new KeyNotFoundException("Không tìm thấy lệnh chặn.");

        _friendRepo.Remove(blockRecord);
        await _friendRepo.SaveChangesAsync();

        _logger.LogInformation(
            "User unblocked: UnblockerId={UserId}, UnblockedId={TargetId}",
            userId, targetId);
    }

    public async Task<PagedResult<FriendListItemDto>> GetFriendsAsync(
        Guid userId, int page, int size)
    {
        // Defensive pagination
        var safePage = page < 1 ? 1 : page;
        var safeSize = size < 1 ? 10 : size > 100 ? 100 : size;

        var query = _friendRepo.Query()
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .Where(fr =>
                fr.Status == FriendStatus.Accepted &&
                (fr.SenderId == userId || fr.ReceiverId == userId));

        var totalCount = await query.CountAsync();

        var records = await query
            .OrderBy(fr =>
                fr.SenderId == userId
                    ? fr.Receiver.FullName
                    : fr.Sender.FullName)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync();

        var items = new List<FriendListItemDto>(records.Count);

        foreach (var fr in records)
        {
            var friend = fr.SenderId == userId ? fr.Receiver : fr.Sender;
            var mutualCount = await _friendRepo.CountMutualFriendsAsync(userId, friend.Id);

            items.Add(new FriendListItemDto
            {
                User = _mapper.Map<UserBriefDto>(friend),
                FriendSince = fr.UpdatedAt,
                MutualFriendsCount = mutualCount
            });
        }

        return PagedResult<FriendListItemDto>.Create(items, totalCount, safePage, safeSize);
    }

    public async Task<PagedResult<FriendResponseDto>> GetPendingRequestsAsync(
        Guid userId, int page, int size)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = size < 1 ? 10 : size > 100 ? 100 : size;

        var query = _friendRepo.Query()
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .Where(fr =>
                fr.ReceiverId == userId &&
                fr.Status == FriendStatus.Pending);

        var totalCount = await query.CountAsync();

        var records = await query
            .OrderByDescending(fr => fr.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync();

        var items = records.Select(MapToFriendResponseDto).ToList();

        return PagedResult<FriendResponseDto>.Create(items, totalCount, safePage, safeSize);
    }

    public async Task<PagedResult<FriendResponseDto>> GetSentRequestsAsync(
        Guid userId, int page, int size)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = size < 1 ? 10 : size > 100 ? 100 : size;

        var query = _friendRepo.Query()
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .Where(fr =>
                fr.SenderId == userId &&
                fr.Status == FriendStatus.Pending);

        var totalCount = await query.CountAsync();

        var records = await query
            .OrderByDescending(fr => fr.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync();

        var items = records.Select(MapToFriendResponseDto).ToList();

        return PagedResult<FriendResponseDto>.Create(items, totalCount, safePage, safeSize);
    }

    public async Task<PagedResult<FriendSuggestionDto>> GetSuggestionsAsync(
        Guid userId, int page, int size)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = size < 1 ? 10 : size > 100 ? 100 : size;

        // Lấy danh sách friendIds của userId
        var friendIds = await _friendRepo.GetFriendIdsAsync(userId);

        // Lấy danh sách userId bị block hoặc đã block
        var blockedIds = await _friendRepo.GetBlockedUserIdsAsync(userId);

        // Lấy user đã có request pending (bất kỳ chiều)
        var pendingIds = await _friendRepo.Query()
            .Where(fr =>
                fr.Status == FriendStatus.Pending &&
                (fr.SenderId == userId || fr.ReceiverId == userId))
            .Select(fr => fr.SenderId == userId ? fr.ReceiverId : fr.SenderId)
            .ToListAsync();

        // Tập loại trừ: chính userId + đã là bạn + đã có pending + blocked bất kỳ chiều
        var excludeIds = new HashSet<Guid>(friendIds) { userId };
        foreach (var id in blockedIds) excludeIds.Add(id);
        foreach (var id in pendingIds) excludeIds.Add(id);

        // Friends-of-friends: lấy friends của từng friend, đếm số lần xuất hiện
        var foFCounts = new Dictionary<Guid, int>();
        var foFMutuals = new Dictionary<Guid, List<Guid>>(); // targetId → list friendIds chung

        foreach (var friendId in friendIds)
        {
            var friendsOfFriend = await _friendRepo.GetFriendIdsAsync(friendId);

            foreach (var candidateId in friendsOfFriend)
            {
                if (excludeIds.Contains(candidateId)) continue;

                if (!foFCounts.ContainsKey(candidateId))
                {
                    foFCounts[candidateId] = 0;
                    foFMutuals[candidateId] = [];
                }

                foFCounts[candidateId]++;
                foFMutuals[candidateId].Add(friendId);
            }
        }

        // Sắp xếp theo số bạn chung giảm dần
        var sortedCandidates = foFCounts
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();

        // Phân trang trên sortedCandidates
        var pagedCandidates = sortedCandidates
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToList();

        // Bổ sung nếu không đủ suggestions — lấy user mới nhất (CreatedAt DESC)
        if (pagedCandidates.Count < safeSize && safePage == 1)
        {
            var needed = safeSize - pagedCandidates.Count;
            var alreadyIncluded = new HashSet<Guid>(sortedCandidates);
            alreadyIncluded.UnionWith(excludeIds);

            var extras = await _userRepo.Query()
                .Where(u => !alreadyIncluded.Contains(u.Id) && u.DeletedAt == null)
                .OrderByDescending(u => u.CreatedAt)
                .Take(needed)
                .ToListAsync();

            foreach (var extra in extras)
            {
                pagedCandidates.Add(extra.Id);
                if (!foFCounts.ContainsKey(extra.Id))
                {
                    foFCounts[extra.Id] = 0;
                    foFMutuals[extra.Id] = [];
                }
            }
        }

        // Tổng count = foF candidates + extras (tính gần đúng cho paging)
        var totalCount = sortedCandidates.Count;

        if (pagedCandidates.Count == 0)
            return PagedResult<FriendSuggestionDto>.Empty(safePage, safeSize);

        // Load user info cho các candidates
        var candidateUsers = await _userRepo.Query()
            .Where(u => pagedCandidates.Contains(u.Id) && u.DeletedAt == null)
            .ToListAsync();

        var userDict = candidateUsers.ToDictionary(u => u.Id);

        // Load mutual friend previews (tối đa 3 người)
        var items = new List<FriendSuggestionDto>(pagedCandidates.Count);

        foreach (var candidateId in pagedCandidates)
        {
            if (!userDict.TryGetValue(candidateId, out var candidateUser)) continue;

            var mutualFriendIdPreview = (foFMutuals.TryGetValue(candidateId, out var mList)
                ? mList
                : [])
                .Distinct()
                .Take(3)
                .ToList();

            var mutualFriendPreviews = new List<UserBriefDto>(mutualFriendIdPreview.Count);

            foreach (var mfId in mutualFriendIdPreview)
            {
                var mfUser = await _userRepo.GetByIdAsync(mfId);
                if (mfUser is not null)
                    mutualFriendPreviews.Add(_mapper.Map<UserBriefDto>(mfUser));
            }

            items.Add(new FriendSuggestionDto
            {
                User = _mapper.Map<UserBriefDto>(candidateUser),
                MutualFriendsCount = foFCounts.GetValueOrDefault(candidateId, 0),
                MutualFriends = mutualFriendPreviews
            });
        }

        return PagedResult<FriendSuggestionDto>.Create(items, totalCount, safePage, safeSize);
    }

    public async Task<FriendStatus> GetFriendshipStatusAsync(Guid userId, Guid targetId)
    {
        if (userId == Guid.Empty || targetId == Guid.Empty)
            throw new ArgumentException("Id người dùng không hợp lệ.");

        var record = await _friendRepo.Query()
            .FirstOrDefaultAsync(fr =>
                (fr.SenderId == userId && fr.ReceiverId == targetId) ||
                (fr.SenderId == targetId && fr.ReceiverId == userId));

        if (record is null)
        {
            // None = 99 — không có record nào
            return (FriendStatus)FriendStatusNone;
        }

        return record.Status;
    }

    public async Task<bool> AreFriendsAsync(Guid userId1, Guid userId2)
    {
        if (userId1 == Guid.Empty || userId2 == Guid.Empty) return false;
        return await _friendRepo.AreFriendsAsync(userId1, userId2);
    }

    private FriendResponseDto MapToFriendResponseDto(FriendRequest fr)
    {
        return new FriendResponseDto
        {
            RequestId = fr.Id,
            Status = fr.Status,
            Sender = _mapper.Map<UserBriefDto>(fr.Sender),
            Receiver = _mapper.Map<UserBriefDto>(fr.Receiver),
            CreatedAt = fr.CreatedAt,
            UpdatedAt = fr.UpdatedAt
        };
    }
}