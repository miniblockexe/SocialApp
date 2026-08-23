using AutoMapper;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Groups;
using SocialApp.Application.DTOs.Posts;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Services;

public sealed class GroupService : IGroupService
{
    private readonly IGroupRepository _groupRepo;
    private readonly IPostRepository _postRepo;
    private readonly ILikeRepository _likeRepo;
    private readonly INotificationService _notifService;
    private readonly ICloudinaryService _cloudinaryService;
    // ✅ Thêm ICloudService để upload media bài đăng (ảnh/video) trong nhóm
    private readonly ICloudService _cloudService;
    private readonly IMapper _mapper;
    private readonly ILogger<GroupService> _logger;

    public GroupService(
        IGroupRepository groupRepo,
        IPostRepository postRepo,
        ILikeRepository likeRepo,
        INotificationService notifService,
        ICloudinaryService cloudinaryService,
        ICloudService cloudService,
        IMapper mapper,
        ILogger<GroupService> logger)
    {
        _groupRepo = groupRepo;
        _postRepo = postRepo;
        _likeRepo = likeRepo;
        _notifService = notifService;
        _cloudinaryService = cloudinaryService;
        _cloudService = cloudService;
        _mapper = mapper;
        _logger = logger;
    }

    // ── CRUD nhóm ──────────────────────────────────────────────────────

    public async Task<GroupDetailDto> CreateGroupAsync(Guid userId, CreateGroupDto dto, CancellationToken ct = default)
    {
        var group = new Group
        {
            OwnerId = userId,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Privacy = dto.Privacy,
            RequireApproval = dto.RequireApproval,
            RequirePostApproval = dto.RequirePostApproval,
        };

        if (dto.Avatar is not null)
        {
            var result = await _cloudinaryService.UploadImageAsync(dto.Avatar, "groups/avatars", maxWidthPx: 400);
            group.AvatarUrl = result.SecureUrl;
            group.AvatarPublicId = result.PublicId;
        }

        await _groupRepo.AddAsync(group, ct);

        var ownerMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = userId,
            Role = GroupRole.Owner,
            JoinedAt = DateTime.UtcNow,
        };
        await _groupRepo.AddMemberAsync(ownerMember, ct);
        await _groupRepo.SaveChangesAsync(ct);

        _logger.LogInformation("[GroupService] Tạo nhóm — GroupId: {GroupId}, OwnerId: {OwnerId}", group.Id, userId);
        return await BuildDetailDtoAsync(group, userId, ct);
    }

    public async Task<GroupDetailDto> UpdateGroupAsync(Guid userId, Guid groupId, UpdateGroupDto dto, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, includeMembers: true, ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        var role = await _groupRepo.GetRoleAsync(groupId, userId, ct);
        if (role is null || role < GroupRole.Admin)
            throw new UnauthorizedAccessException("Chỉ admin/owner mới có thể cập nhật thông tin nhóm.");

        if (dto.Name is not null) group.Name = dto.Name.Trim();
        if (dto.Description is not null) group.Description = dto.Description.Trim();
        if (dto.Privacy.HasValue) group.Privacy = dto.Privacy.Value;
        if (dto.RequireApproval.HasValue) group.RequireApproval = dto.RequireApproval.Value;
        if (dto.RequirePostApproval.HasValue) group.RequirePostApproval = dto.RequirePostApproval.Value;

        if (dto.Avatar is not null)
        {
            if (group.AvatarPublicId is not null)
                await _cloudinaryService.DeleteAsync(group.AvatarPublicId, ResourceType.Image);
            var result = await _cloudinaryService.UploadImageAsync(dto.Avatar, "groups/avatars", maxWidthPx: 400);
            group.AvatarUrl = result.SecureUrl;
            group.AvatarPublicId = result.PublicId;
        }

        if (dto.Cover is not null)
        {
            if (group.CoverPublicId is not null)
                await _cloudinaryService.DeleteAsync(group.CoverPublicId, ResourceType.Image);
            var result = await _cloudinaryService.UploadImageAsync(dto.Cover, "groups/covers", maxWidthPx: 1200);
            group.CoverUrl = result.SecureUrl;
            group.CoverPublicId = result.PublicId;
        }

        await _groupRepo.SaveChangesAsync(ct);
        return await BuildDetailDtoAsync(group, userId, ct);
    }

    public async Task DeleteGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        if (group.OwnerId != userId)
            throw new UnauthorizedAccessException("Chỉ owner mới có thể xóa nhóm.");

        group.DeletedAt = DateTime.UtcNow;
        await _groupRepo.SaveChangesAsync(ct);
    }

    public async Task<GroupDetailDto> GetGroupAsync(Guid groupId, Guid viewerId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, includeMembers: true, ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");
        return await BuildDetailDtoAsync(group, viewerId, ct);
    }

    public async Task<PagedResult<GroupSummaryDto>> SearchGroupsAsync(Guid viewerId, string? keyword, int page, int size, CancellationToken ct = default)
    {
        size = Math.Min(size, 50);
        var (items, total) = await _groupRepo.SearchGroupsAsync(keyword, page, size, ct);
        var dtos = await MapSummaryListAsync(items, viewerId, ct);
        return PagedResult<GroupSummaryDto>.Create(dtos, total, page, size);
    }

    public async Task<PagedResult<GroupSummaryDto>> GetMyGroupsAsync(Guid userId, int page, int size, CancellationToken ct = default)
    {
        size = Math.Min(size, 50);
        var (items, total) = await _groupRepo.GetUserGroupsAsync(userId, page, size, ct);
        var dtos = await MapSummaryListAsync(items, userId, ct);
        return PagedResult<GroupSummaryDto>.Create(dtos, total, page, size);
    }

    // ── Member ─────────────────────────────────────────────────────────

    public async Task<object> JoinGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        if (await _groupRepo.IsMemberAsync(groupId, userId, ct))
            throw new InvalidOperationException("Bạn đã là thành viên của nhóm này.");

        var existingReq = await _groupRepo.GetJoinRequestAsync(groupId, userId, ct);
        if (existingReq?.Status == JoinRequestStatus.Pending)
            throw new InvalidOperationException("Bạn đã có đơn tham gia đang chờ duyệt.");

        if (group.RequireApproval)
        {
            var joinRequest = new GroupJoinRequest { GroupId = groupId, UserId = userId };
            await _groupRepo.AddJoinRequestAsync(joinRequest, ct);
            await _groupRepo.SaveChangesAsync(ct);

            await _notifService.CreateNotificationAsync(
                group.OwnerId, userId, NotificationType.System, groupId,
                $"Có người muốn tham gia nhóm \"{group.Name}\".");

            return new GroupJoinRequestDto
            {
                Id = joinRequest.Id,
                User = null!,
                Status = JoinRequestStatus.Pending,
                CreatedAt = joinRequest.CreatedAt,
            };
        }
        else
        {
            var member = new GroupMember { GroupId = groupId, UserId = userId, Role = GroupRole.Member };
            await _groupRepo.AddMemberAsync(member, ct);
            await _groupRepo.SaveChangesAsync(ct);

            return new GroupMemberDto
            {
                User = null!,
                Role = GroupRole.Member,
                JoinedAt = member.JoinedAt,
            };
        }
    }

    public async Task LeaveGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        if (group.OwnerId == userId)
            throw new InvalidOperationException("Owner không thể rời nhóm. Hãy chuyển quyền owner trước.");

        var member = await _groupRepo.GetMemberAsync(groupId, userId, ct)
            ?? throw new KeyNotFoundException("Bạn không phải thành viên của nhóm này.");

        await _groupRepo.RemoveMemberAsync(member);
        await _groupRepo.SaveChangesAsync(ct);
    }

    public async Task KickMemberAsync(Guid requesterId, Guid groupId, Guid targetUserId, CancellationToken ct = default)
    {
        var requesterRole = await _groupRepo.GetRoleAsync(groupId, requesterId, ct)
            ?? throw new UnauthorizedAccessException("Bạn không phải thành viên của nhóm này.");

        if (requesterRole < GroupRole.Admin)
            throw new UnauthorizedAccessException("Chỉ admin/owner mới có thể kick thành viên.");

        var targetMember = await _groupRepo.GetMemberAsync(groupId, targetUserId, ct)
            ?? throw new KeyNotFoundException("Thành viên không tồn tại trong nhóm.");

        if (targetMember.Role == GroupRole.Owner)
            throw new InvalidOperationException("Không thể kick owner.");

        if (targetMember.Role == GroupRole.Admin && requesterRole < GroupRole.Owner)
            throw new UnauthorizedAccessException("Chỉ owner mới có thể kick admin.");

        await _groupRepo.RemoveMemberAsync(targetMember);
        await _groupRepo.SaveChangesAsync(ct);
    }

    public async Task UpdateMemberRoleAsync(Guid requesterId, Guid groupId, Guid targetUserId, GroupRole newRole, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        if (group.OwnerId != requesterId)
            throw new UnauthorizedAccessException("Chỉ owner mới có thể thay đổi vai trò thành viên.");

        if (targetUserId == requesterId)
            throw new InvalidOperationException("Không thể thay đổi vai trò của chính mình.");

        if (newRole == GroupRole.Owner)
            throw new InvalidOperationException("Không thể gán Owner trực tiếp. Dùng chức năng Transfer Ownership.");

        var targetMember = await _groupRepo.GetMemberAsync(groupId, targetUserId, ct)
            ?? throw new KeyNotFoundException("Thành viên không tồn tại trong nhóm.");

        targetMember.Role = newRole;
        await _groupRepo.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<GroupMemberDto>> GetMembersAsync(Guid requesterId, Guid groupId, int page, int size, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        if (group.Privacy == GroupPrivacy.Private && !await _groupRepo.IsMemberAsync(groupId, requesterId, ct))
            throw new UnauthorizedAccessException("Chỉ thành viên mới xem được danh sách thành viên nhóm riêng tư.");

        size = Math.Min(size, 50);
        var members = await _groupRepo.GetMembersPagedAsync(groupId, page, size, ct);
        var total = await _groupRepo.GetMemberCountAsync(groupId, ct);
        var dtos = members.Select(m => _mapper.Map<GroupMemberDto>(m)).ToList();
        return PagedResult<GroupMemberDto>.Create(dtos, total, page, size);
    }

    // ── Join Request ───────────────────────────────────────────────────

    public async Task ReviewJoinRequestAsync(Guid reviewerId, Guid groupId, Guid requestId, ApproveJoinRequestDto dto, CancellationToken ct = default)
    {
        var role = await _groupRepo.GetRoleAsync(groupId, reviewerId, ct)
            ?? throw new UnauthorizedAccessException("Bạn không phải thành viên của nhóm này.");

        if (role < GroupRole.Admin)
            throw new UnauthorizedAccessException("Chỉ admin/owner mới có thể duyệt đơn tham gia.");

        var request = await _groupRepo.GetJoinRequestByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Đơn tham gia không tồn tại.");

        if (request.GroupId != groupId)
            throw new KeyNotFoundException("Đơn tham gia không thuộc nhóm này.");

        if (request.Status != JoinRequestStatus.Pending)
            throw new InvalidOperationException("Đơn này đã được xử lý.");

        request.ReviewedByUserId = reviewerId;
        request.UpdatedAt = DateTime.UtcNow;

        if (dto.Approve)
        {
            request.Status = JoinRequestStatus.Approved;
            await _groupRepo.AddMemberAsync(new GroupMember
            {
                GroupId = groupId,
                UserId = request.UserId,
                Role = GroupRole.Member,
                JoinedAt = DateTime.UtcNow,
            }, ct);

            var group = await _groupRepo.GetByIdAsync(groupId, ct: ct);
            await _notifService.CreateNotificationAsync(
                request.UserId, reviewerId, NotificationType.System, groupId,
                $"Đơn tham gia nhóm \"{group?.Name}\" của bạn đã được chấp nhận.");
        }
        else
        {
            request.Status = JoinRequestStatus.Rejected;
            request.RejectReason = dto.RejectReason;
        }

        await _groupRepo.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<GroupJoinRequestDto>> GetPendingJoinRequestsAsync(Guid requesterId, Guid groupId, int page, int size, CancellationToken ct = default)
    {
        var role = await _groupRepo.GetRoleAsync(groupId, requesterId, ct)
            ?? throw new UnauthorizedAccessException("Bạn không phải thành viên của nhóm này.");

        if (role < GroupRole.Admin)
            throw new UnauthorizedAccessException("Chỉ admin/owner mới xem được danh sách đơn chờ duyệt.");

        size = Math.Min(size, 50);
        var requests = await _groupRepo.GetPendingRequestsPagedAsync(groupId, page, size, ct);
        var total = await _groupRepo.GetPendingRequestCountAsync(groupId, ct);
        var dtos = requests.Select(r => _mapper.Map<GroupJoinRequestDto>(r)).ToList();
        return PagedResult<GroupJoinRequestDto>.Create(dtos, total, page, size);
    }

    public async Task CancelJoinRequestAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var request = await _groupRepo.GetJoinRequestAsync(groupId, userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn tham gia.");

        if (request.Status != JoinRequestStatus.Pending)
            throw new InvalidOperationException("Không thể hủy đơn đã được xử lý.");

        request.Status = JoinRequestStatus.Rejected;
        request.UpdatedAt = DateTime.UtcNow;
        await _groupRepo.SaveChangesAsync(ct);
    }

    // ── Group Post ─────────────────────────────────────────────────────

    public async Task<PostResponseDto> CreateGroupPostAsync(Guid userId, Guid groupId, CreateGroupPostDto dto, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        var role = await _groupRepo.GetRoleAsync(groupId, userId, ct);

        if (group.Privacy == GroupPrivacy.Private && role is null)
            throw new UnauthorizedAccessException("Chỉ thành viên mới có thể đăng bài vào nhóm riêng tư.");

        // Public group: auto-join khi đăng bài nếu chưa là thành viên
        if (group.Privacy == GroupPrivacy.Public && role is null)
        {
            await _groupRepo.AddMemberAsync(new GroupMember
            {
                GroupId = groupId,
                UserId = userId,
                Role = GroupRole.Member
            }, ct);
            role = GroupRole.Member;
        }

        var requireApproval = group.RequirePostApproval && role < GroupRole.Admin;

        var post = new Post
        {
            UserId = userId,
            Content = dto.Content?.Trim(),
            Privacy = dto.Privacy,
            GroupId = groupId,
        };

        await _postRepo.AddAsync(post, ct);
        await _postRepo.SaveChangesAsync(ct); // lưu trước để có post.Id cho folder upload cloud

        // ✅ Upload media files nếu có (fix: trước đây bỏ qua hoàn toàn dto.MediaFiles)
        var files = dto.MediaFiles;
        if (files is not null && files.Count > 0)
        {
            var uploaded = new List<(string Url, string PublicId, long Size, MediaType MediaType, StorageProvider StorageProvider)>();

            try
            {
                var uploadTasks = files.Select(f => _cloudService.UploadMediaAsync(f, $"posts/{post.Id}", ct));
                var results = await Task.WhenAll(uploadTasks);
                uploaded.AddRange(results.Select(r => (r.SecureUrl, r.PublicId, r.FileSize, r.MediaType, r.StorageProvider)));

                var mediaFiles = results.Select(r => new PostMediaFile
                {
                    PostId = post.Id,
                    MediaUrl = r.SecureUrl,
                    PublicId = r.PublicId,
                    MediaType = r.MediaType,
                    StorageProvider = r.StorageProvider,
                    FileSize = r.FileSize,
                });

                await _postRepo.AddMediaFilesAsync(mediaFiles, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[GroupService] Upload media thất bại, rollback — PostId: {PostId}, GroupId: {GroupId}",
                    post.Id, groupId);

                // Cleanup các file đã upload thành công trước khi throw
                foreach (var (_, publicId, _, mediaType, storageProvider) in uploaded)
                {
                    try { await _cloudService.DeleteMediaAsync(publicId, storageProvider, mediaType); }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx,
                            "[GroupService] Cleanup file cloud thất bại — PublicId: {PublicId}", publicId);
                    }
                }

                // Soft-delete post vừa tạo để tránh orphan record
                try { await _postRepo.ExecuteSoftDeleteAsync(p => p.Id == post.Id); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx,
                        "[GroupService] Rollback DB thất bại — PostId: {PostId}. Post bị orphan.", post.Id);
                }

                throw new InvalidOperationException("Upload thất bại, vui lòng thử lại sau.");
            }
        }

        var groupPost = new GroupPost
        {
            PostId = post.Id,
            GroupId = groupId,
            Status = requireApproval ? GroupPostStatus.Pending : GroupPostStatus.Approved,
        };
        await _groupRepo.AddGroupPostAsync(groupPost, ct);
        await _groupRepo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[GroupService] Tạo bài nhóm — PostId: {PostId}, GroupId: {GroupId}, Pending: {Pending}",
            post.Id, groupId, requireApproval);

        // Load lại post đầy đủ (bao gồm MediaFiles) để trả về đúng data
        var savedPost = await _postRepo.FirstOrDefaultAsync(
            p => p.Id == post.Id, ct, p => p.User, p => p.PostMediaFiles);

        return new PostResponseDto
        {
            Id = savedPost!.Id,
            Content = savedPost.Content,
            Privacy = savedPost.Privacy,
            CreatedAt = savedPost.CreatedAt,
            UpdatedAt = savedPost.UpdatedAt,
            Author = _mapper.Map<UserBriefDto>(savedPost.User),
            // ✅ Trả về đúng MediaFiles đã upload thay vì []
            MediaFiles = savedPost.PostMediaFiles.Select(mf => new PostMediaDto
            {
                Id = mf.Id,
                MediaUrl = mf.MediaUrl,
                MediaType = mf.MediaType,
                StorageProvider = mf.StorageProvider,
                FileSize = mf.FileSize,
            }).ToList(),
            LikeCount = 0,
            CommentCount = 0,
            IsLikedByMe = false,
            IsOwner = true,
            ShareCount = 0,
            IsSharedByMe = false,
            // ✅ Trả về GroupId và GroupName để frontend hiển thị "Tên người đăng › Tên nhóm"
            GroupId = groupId,
            GroupName = group.Name,
        };
    }

    public async Task<PagedResult<PostResponseDto>> GetGroupFeedAsync(Guid viewerId, Guid groupId, int page, int size, Guid? cursorId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct)
            ?? throw new KeyNotFoundException("Nhóm không tồn tại.");

        if (group.Privacy == GroupPrivacy.Private && !await _groupRepo.IsMemberAsync(groupId, viewerId, ct))
            throw new UnauthorizedAccessException("Chỉ thành viên mới xem được bài đăng của nhóm riêng tư.");

        size = Math.Min(size, 50);
        var posts = await _groupRepo.GetGroupFeedAsync(groupId, size, cursorId, ct);

        var postIds = posts.Select(p => p.Id).ToList();
        var likes = await _likeRepo.GetByPostIdsAsync(postIds, ct);
        var likedSet = likes.Where(l => l.UserId == viewerId).Select(l => l.PostId).ToHashSet();
        var likeCountMap = likes.GroupBy(l => l.PostId).ToDictionary(g => g.Key, g => g.Count());

        var dtos = posts.Select(p => new PostResponseDto
        {
            Id = p.Id,
            Content = p.Content,
            Privacy = p.Privacy,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Author = _mapper.Map<UserBriefDto>(p.User),
            MediaFiles = p.PostMediaFiles.Select(mf => new PostMediaDto
            {
                Id = mf.Id,
                MediaUrl = mf.MediaUrl,
                MediaType = mf.MediaType,
                StorageProvider = mf.StorageProvider,
                FileSize = mf.FileSize,
            }).ToList(),
            LikeCount = likeCountMap.GetValueOrDefault(p.Id),
            CommentCount = p.Comments.Count(c => c.DeletedAt == null),
            IsLikedByMe = likedSet.Contains(p.Id),
            IsOwner = p.UserId == viewerId,
            ShareCount = 0,
            IsSharedByMe = false,
            // ✅ Gán GroupId và GroupName vào từng bài để post-card hiển thị "Tên người đăng › Tên nhóm"
            GroupId = groupId,
            GroupName = group.Name,
        }).ToList();

        return PagedResult<PostResponseDto>.Create(dtos, dtos.Count, page, size);
    }

    public async Task<PagedResult<PostResponseDto>> GetPendingPostsAsync(Guid requesterId, Guid groupId, int page, int size, CancellationToken ct = default)
    {
        var role = await _groupRepo.GetRoleAsync(groupId, requesterId, ct)
            ?? throw new UnauthorizedAccessException("Bạn không phải thành viên của nhóm này.");

        if (role < GroupRole.Admin)
            throw new UnauthorizedAccessException("Chỉ admin/owner mới xem được bài chờ duyệt.");

        size = Math.Min(size, 50);
        var posts = await _groupRepo.GetPendingPostsPagedAsync(groupId, page, size, ct);

        // ✅ Load tên nhóm một lần để gán vào tất cả bài trong batch
        var group = await _groupRepo.GetByIdAsync(groupId, ct: ct);

        var dtos = posts.Select(p => new PostResponseDto
        {
            Id = p.Id,
            Content = p.Content,
            Privacy = p.Privacy,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Author = _mapper.Map<UserBriefDto>(p.User),
            MediaFiles = p.PostMediaFiles.Select(mf => new PostMediaDto
            {
                Id = mf.Id,
                MediaUrl = mf.MediaUrl,
                MediaType = mf.MediaType,
                StorageProvider = mf.StorageProvider,
                FileSize = mf.FileSize,
            }).ToList(),
            LikeCount = 0,
            CommentCount = 0,
            IsLikedByMe = false,
            IsOwner = p.UserId == requesterId,
            ShareCount = 0,
            IsSharedByMe = false,
            // ✅ Gán GroupId và GroupName
            GroupId = groupId,
            GroupName = group?.Name,
        }).ToList();

        return PagedResult<PostResponseDto>.Create(dtos, dtos.Count, page, size);
    }

    public async Task ReviewGroupPostAsync(Guid reviewerId, Guid groupId, Guid postId, ReviewGroupPostDto dto, CancellationToken ct = default)
    {
        var role = await _groupRepo.GetRoleAsync(groupId, reviewerId, ct)
            ?? throw new UnauthorizedAccessException("Bạn không phải thành viên của nhóm này.");

        if (role < GroupRole.Admin)
            throw new UnauthorizedAccessException("Chỉ admin/owner mới có thể duyệt bài đăng.");

        var groupPost = await _groupRepo.GetGroupPostAsync(postId, groupId, ct)
            ?? throw new KeyNotFoundException("Bài đăng không tồn tại trong nhóm.");

        if (groupPost.Status != GroupPostStatus.Pending)
            throw new InvalidOperationException("Bài này đã được xử lý.");

        groupPost.Status = dto.Approve ? GroupPostStatus.Approved : GroupPostStatus.Rejected;
        groupPost.ReviewedByUserId = reviewerId;
        await _groupRepo.SaveChangesAsync(ct);

        if (dto.Approve)
        {
            var group = await _groupRepo.GetByIdAsync(groupId, ct: ct);
            await _notifService.CreateNotificationAsync(
                groupPost.Post.UserId, reviewerId, NotificationType.System, postId,
                $"Bài đăng của bạn trong nhóm \"{group?.Name}\" đã được phê duyệt.");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private async Task<GroupDetailDto> BuildDetailDtoAsync(Group group, Guid viewerId, CancellationToken ct)
    {
        var memberCount = await _groupRepo.GetMemberCountAsync(group.Id, ct);
        var role = await _groupRepo.GetRoleAsync(group.Id, viewerId, ct);
        var membership = await GetMembershipStatusAsync(group.Id, viewerId, role, ct);

        var dto = _mapper.Map<GroupDetailDto>(group);
        dto.MemberCount = memberCount;
        dto.ViewerRole = role;
        dto.MembershipStatus = membership;

        var allMembers = await _groupRepo.GetMembersPagedAsync(group.Id, 1, 100, ct);
        dto.Admins = allMembers
            .Where(m => m.Role >= GroupRole.Admin)
            .Select(m => _mapper.Map<GroupMemberDto>(m))
            .ToList();

        return dto;
    }

    private async Task<GroupMembershipStatus> GetMembershipStatusAsync(Guid groupId, Guid userId, GroupRole? role, CancellationToken ct)
    {
        if (role is not null) return GroupMembershipStatus.Member;
        var req = await _groupRepo.GetJoinRequestAsync(groupId, userId, ct);
        return req?.Status == JoinRequestStatus.Pending
            ? GroupMembershipStatus.PendingApproval
            : GroupMembershipStatus.None;
    }

    private async Task<List<GroupSummaryDto>> MapSummaryListAsync(List<Group> groups, Guid viewerId, CancellationToken ct)
    {
        var result = new List<GroupSummaryDto>();
        foreach (var g in groups)
        {
            var memberCount = await _groupRepo.GetMemberCountAsync(g.Id, ct);
            var role = await _groupRepo.GetRoleAsync(g.Id, viewerId, ct);
            var membership = await GetMembershipStatusAsync(g.Id, viewerId, role, ct);

            var dto = _mapper.Map<GroupSummaryDto>(g);
            dto.MemberCount = memberCount;
            dto.ViewerRole = role;
            dto.MembershipStatus = membership;
            result.Add(dto);
        }
        return result;
    }
}