using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Posts;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;
using AutoMapper;
using System;

namespace SocialApp.Application.Services;

/// <summary>
/// Implement IPostService: CRUD bài đăng, feed, toggle like, comment (reply 1 cấp).
/// Media hỗ trợ ảnh (Cloudinary) + video/audio (Cloudflare R2), phân loại theo Content-Type
/// qua FileValidationSettings.
/// </summary>
public sealed class PostService : IPostService
{
    private readonly IPostRepository _postRepo;
    private readonly IGenericRepository<Comment> _commentRepo;
    private readonly ILikeRepository _likeRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly IFriendRequestRepository _friendRepo;
    // ✅ Inject IGroupRepository để lấy danh sách nhóm viewer đã tham gia (dùng trong GetFeedAsync)
    private readonly IGroupRepository _groupRepo;
    private readonly ICloudService _cloudService;
    private readonly IMapper _mapper;
    private readonly ILogger<PostService> _logger;
    private readonly FileValidationSettings _fileValidation;
    private readonly CloudflareR2Settings _r2Settings;
    private readonly IProfanityFilterService _profanityFilter;

    private const long MaxImageBytes = 200 * 1024 * 1024; // 200MB

    public PostService(
        IPostRepository postRepo,
        IGenericRepository<Comment> commentRepo,
        ILikeRepository likeRepo,
        INotificationRepository notificationRepo,
        IFriendRequestRepository friendRepo,
        IGroupRepository groupRepo,
        ICloudService cloudService,
        IMapper mapper,
        ILogger<PostService> logger,
        IOptions<FileValidationSettings> fileValidationOptions,
        IOptions<CloudflareR2Settings> r2Options,
        IProfanityFilterService profanityFilter)
    {
        _postRepo = postRepo;
        _commentRepo = commentRepo;
        _likeRepo = likeRepo;
        _notificationRepo = notificationRepo;
        _friendRepo = friendRepo;
        _groupRepo = groupRepo;
        _cloudService = cloudService;
        _mapper = mapper;
        _logger = logger;
        _fileValidation = fileValidationOptions.Value;
        _r2Settings = r2Options.Value;
        _profanityFilter = profanityFilter;
    }

    public async Task<PostResponseDto> CreatePostAsync(Guid userId, CreatePostDto dto)
    {
        var content = dto.Content?.Trim();
        var hasContent = !string.IsNullOrWhiteSpace(content);
        var files = dto.MediaFiles;
        var hasMedia = files is not null && files.Count > 0;

        if (!hasContent && !hasMedia)
            throw new ArgumentException("Bài đăng phải có nội dung hoặc ít nhất 1 file media.");

        if (hasContent && await _profanityFilter.ContainsProfanityAsync(content!))
        {
            _logger.LogWarning("[CreatePostAsync] Profanity detected — UserId: {UserId}", userId);
            throw new ArgumentException("Nội dung bài đăng chứa từ ngữ không phù hợp.");
        }

        if (hasMedia)
        {
            foreach (var file in files!)
                ValidatePostMedia(file);
        }

        var post = new Post
        {
            UserId = userId,
            Content = hasContent ? content : null,
            Privacy = dto.Privacy
        };

        await _postRepo.AddAsync(post);
        await _postRepo.SaveChangesAsync();

        if (hasMedia)
        {
            var uploaded = new List<PostMediaUploadResult>();

            try
            {
                var results = await Task.WhenAll(
                    files!.Select((f, index) => UploadPostMediaAsync(f, post.Id, index)));
                uploaded.AddRange(results);

                var mediaFiles = results.Select(r => new PostMediaFile
                {
                    PostId = post.Id,
                    MediaUrl = r.Url,
                    PublicId = r.PublicId,
                    MediaType = r.MediaType,
                    StorageProvider = r.StorageProvider,
                    FileSize = r.Size
                });

                await _postRepo.AddMediaFilesAsync(mediaFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[CreatePostAsync] Upload media thất bại, rollback — PostId: {PostId}, UserId: {UserId}",
                    post.Id, userId);

                foreach (var r in uploaded)
                {
                    try { await _cloudService.DeleteMediaAsync(r.PublicId, r.StorageProvider, r.MediaType); }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx,
                            "[CreatePostAsync] Cleanup file cloud thất bại — PublicId: {PublicId}", r.PublicId);
                    }
                }

                try { await _postRepo.ExecuteSoftDeleteAsync(p => p.Id == post.Id); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx,
                        "[CreatePostAsync] Rollback DB thất bại — PostId: {PostId}. Post bị orphan.", post.Id);
                }

                throw new InvalidOperationException("Upload thất bại, vui lòng thử lại sau.");
            }
        }

        var savedPost = await _postRepo.FirstOrDefaultAsync(
            p => p.Id == post.Id, default, p => p.User, p => p.PostMediaFiles);

        return await BuildPostResponseAsync(savedPost!, userId);
    }

    public async Task<PostResponseDto> UpdatePostAsync(Guid userId, Guid postId, UpdatePostDto dto)
    {
        var post = await _postRepo.FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null);
        if (post is null)
            throw new KeyNotFoundException($"Bài đăng {postId} không tồn tại.");

        if (post.UserId != userId)
            throw new ForbiddenException("Bạn không phải chủ bài viết này.");

        if (!string.IsNullOrWhiteSpace(dto.Content))
            post.Content = dto.Content.Trim();

        if (dto.Privacy.HasValue)
            post.Privacy = dto.Privacy.Value;

        post.UpdatedAt = DateTime.UtcNow;

        _postRepo.Update(post);
        await _postRepo.SaveChangesAsync();

        var savedPost = await _postRepo.FirstOrDefaultAsync(
            p => p.Id == postId, default, p => p.User, p => p.PostMediaFiles);

        return await BuildPostResponseAsync(savedPost!, userId);
    }

    public async Task DeletePostAsync(Guid userId, Guid postId, bool isAdmin = false)
    {
        var post = await _postRepo.FirstOrDefaultAsync(p => p.Id == postId);
        if (post is null)
            throw new KeyNotFoundException($"Bài đăng {postId} không tồn tại.");

        if (!isAdmin && post.UserId != userId)
            throw new ForbiddenException("Bạn không phải chủ bài viết này.");

        if (post.IsDeleted)
            throw new InvalidOperationException("Bài viết đã được xóa trước đó.");

        _postRepo.Remove(post);
        await _postRepo.SaveChangesAsync();

        if (isAdmin)
        {
            _logger.LogWarning(
                "[DeletePostAsync] Admin xóa bài viết — PostId: {PostId}, AdminId: {AdminId}, TacGiaGoc: {AuthorId}",
                postId, userId, post.UserId);
        }
    }

    public async Task<PostResponseDto> GetPostByIdAsync(Guid postId, Guid viewerId)
    {
        var post = await _postRepo.FirstOrDefaultAsync(
            p => p.Id == postId && p.DeletedAt == null, default, p => p.User, p => p.PostMediaFiles);

        if (post is null)
            throw new KeyNotFoundException($"Bài đăng {postId} không tồn tại.");

        await EnsureViewableAsync(post, viewerId);

        return await BuildPostResponseAsync(post, viewerId);
    }

    public async Task<PagedResult<PostResponseDto>> GetFeedAsync(Guid userId, FeedQueryDto query)
    {
        var friendIds = (IReadOnlyCollection<Guid>)await _friendRepo.GetFriendIdsAsync(userId);
        var blockedIds = (IReadOnlyCollection<Guid>)await _friendRepo.GetBlockedUserIdsAsync(userId);
        var memberGroupIds = (IReadOnlyCollection<Guid>)await _groupRepo.GetUserGroupIdsAsync(userId);

        // Resolve cursor nếu có (infinite scroll)
        DateTime? cursorCreatedAt = null;
        if (query.CursorId.HasValue)
        {
            var cursorPost = await _postRepo.GetByIdAsync(query.CursorId.Value);
            cursorCreatedAt = cursorPost?.CreatedAt;
        }

        // ✅ Dùng GetFeedPostsAsync thay vì GetPagedAsync để có đầy đủ group privacy logic:
        //   - Bài cá nhân: lọc theo Privacy + friends như cũ
        //   - Bài public group (Approved): hiện cho tất cả
        //   - Bài private group (Approved): chỉ hiện cho thành viên
        //   - Bài Pending/Rejected: ẩn hoàn toàn khỏi feed
        var (items, totalCount) = await _postRepo.GetFeedPostsAsync(
            userId,
            friendIds,
            blockedIds,
            memberGroupIds,
            query.Page,
            query.Size,
            cursorCreatedAt);

        var dtos = await BuildPostResponsesAsync(items, userId);

        return PagedResult<PostResponseDto>.Create(dtos, totalCount, query.Page, query.Size);
    }

    public async Task<PagedResult<PostResponseDto>> GetUserPostsAsync(
        Guid targetId, Guid viewerId, int page, int size)
    {
        var isFriend = targetId == viewerId || await _friendRepo.AreFriendsAsync(viewerId, targetId);

        var pagedQuery = new PagedQuery { PageNumber = page, PageSize = size };

        var result = await _postRepo.GetPagedAsync(
            pagedQuery,
            predicate: p =>
                p.UserId == targetId &&
                p.DeletedAt == null &&
                (
                    p.UserId == viewerId ||
                    p.Privacy == PostPrivacy.Public ||
                    (p.Privacy == PostPrivacy.Friends && isFriend)
                ),
            orderBy: p => p.CreatedAt,
            ct: default,
            includes: [p => p.User, p => p.PostMediaFiles, p => p.Group!]);

        var dtos = await BuildPostResponsesAsync(result.Items, viewerId);

        return PagedResult<PostResponseDto>.Create(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<bool> ToggleLikeAsync(Guid userId, Guid postId)
    {
        var post = await _postRepo.FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null);
        if (post is null)
            throw new KeyNotFoundException($"Bài đăng {postId} không tồn tại.");

        await EnsureViewableAsync(post, userId);

        var existingLike = await _likeRepo.GetByUserAndPostAsync(userId, postId);

        if (existingLike is not null)
        {
            _likeRepo.Remove(existingLike);
            await _likeRepo.SaveChangesAsync();
            return false;
        }

        try
        {
            await _likeRepo.AddAsync(new Like
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PostId = postId,
                CreatedAt = DateTime.UtcNow
            });
            await _likeRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg
                  && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _logger.LogWarning(
                "ToggleLike race condition: like đã tồn tại (userId={UserId}, postId={PostId}). Chuyển sang unlike.",
                userId, postId);

            var duplicate = await _likeRepo.GetByUserAndPostAsync(userId, postId);
            if (duplicate is not null)
            {
                _likeRepo.Remove(duplicate);
                await _likeRepo.SaveChangesAsync();
            }
            return false;
        }

        if (userId != post.UserId)
        {
            await CreateNotificationAsync(
                recipientId: post.UserId,
                actorId: userId,
                type: NotificationType.Like,
                entityId: postId,
                content: "đã thích bài viết của bạn.");
        }

        return true;
    }

    public async Task<PagedResult<CommentResponseDto>> GetCommentsAsync(
        Guid postId, Guid viewerId, int page, int size)
    {
        var postExists = await _postRepo.ExistsAsync(p => p.Id == postId && p.DeletedAt == null);
        if (!postExists)
            throw new KeyNotFoundException($"Bài đăng {postId} không tồn tại.");

        var pagedQuery = new PagedQuery { PageNumber = page, PageSize = size };

        var result = await _commentRepo.GetPagedAsync(
            pagedQuery,
            predicate: c => c.PostId == postId && c.DeletedAt == null && c.ParentCommentId == null,
            orderBy: c => c.CreatedAt,
            ct: default,
            includes: [c => c.User]);

        var commentIds = result.Items.Select(c => c.Id).ToList();

        var replyCounts = (await _commentRepo.GetAsync(c =>
                c.ParentCommentId != null &&
                commentIds.Contains(c.ParentCommentId!.Value) &&
                c.DeletedAt == null))
            .ToLookup(c => c.ParentCommentId!.Value);

        var dtos = result.Items.Select(c =>
        {
            var d = _mapper.Map<CommentResponseDto>(c);
            d.RepliesCount = replyCounts[c.Id].Count();
            d.IsOwner = c.UserId == viewerId;
            return d;
        }).ToList();

        return PagedResult<CommentResponseDto>.Create(dtos, result.TotalCount, result.PageNumber, result.PageSize);
    }

    public async Task<CommentResponseDto> AddCommentAsync(Guid userId, Guid postId, CreateCommentDto dto)
    {
        var post = await _postRepo.FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null);
        if (post is null)
            throw new KeyNotFoundException($"Bài đăng {postId} không tồn tại.");

        Comment? parent = null;

        if (dto.ParentCommentId.HasValue)
        {
            parent = await _commentRepo.FirstOrDefaultAsync(
                c => c.Id == dto.ParentCommentId.Value && c.DeletedAt == null);

            if (parent is null)
                throw new KeyNotFoundException($"Bình luận gốc {dto.ParentCommentId.Value} không tồn tại.");

            if (parent.ParentCommentId is not null)
                throw new ArgumentException("Không thể reply vào reply, chỉ hỗ trợ 1 cấp.");

            if (parent.PostId != postId)
                throw new ArgumentException("Bình luận gốc không thuộc bài đăng này.");
        }

        var trimmedCommentContent = dto.Content.Trim();
        if (await _profanityFilter.ContainsProfanityAsync(trimmedCommentContent))
        {
            _logger.LogWarning("[AddCommentAsync] Profanity detected — UserId: {UserId}, PostId: {PostId}", userId, postId);
            throw new ArgumentException("Nội dung bình luận chứa từ ngữ không phù hợp.");
        }

        var comment = new Comment
        {
            PostId = postId,
            UserId = userId,
            ParentCommentId = dto.ParentCommentId,
            Content = trimmedCommentContent
        };

        await _commentRepo.AddAsync(comment);
        await _commentRepo.SaveChangesAsync();

        var recipientId = parent?.UserId ?? post.UserId;
        if (recipientId != userId)
        {
            await CreateNotificationAsync(
                recipientId: recipientId,
                actorId: userId,
                type: NotificationType.Comment,
                entityId: postId,
                content: parent is not null
                    ? "đã trả lời bình luận của bạn."
                    : "đã bình luận về bài viết của bạn.");
        }

        var savedComment = await _commentRepo.FirstOrDefaultAsync(
            c => c.Id == comment.Id, default, c => c.User);

        var responseDto = _mapper.Map<CommentResponseDto>(savedComment);
        responseDto.RepliesCount = 0;
        responseDto.IsOwner = true;
        return responseDto;
    }

    public async Task DeleteCommentAsync(Guid userId, Guid commentId, bool isAdmin = false)
    {
        var comment = await _commentRepo.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null)
            throw new KeyNotFoundException($"Bình luận {commentId} không tồn tại.");

        if (!isAdmin && comment.UserId != userId)
            throw new ForbiddenException("Bạn không phải chủ bình luận này.");

        if (comment.IsDeleted)
            throw new InvalidOperationException("Bình luận đã được xóa trước đó.");

        _commentRepo.Remove(comment);
        await _commentRepo.SaveChangesAsync();

        if (isAdmin)
        {
            _logger.LogWarning(
                "[DeleteCommentAsync] Admin xóa bình luận — CommentId: {CommentId}, AdminId: {AdminId}, TacGiaGoc: {AuthorId}",
                commentId, userId, comment.UserId);
        }
    }

    private async Task EnsureViewableAsync(Post post, Guid viewerId)
    {
        if (post.UserId == viewerId) return;

        switch (post.Privacy)
        {
            case PostPrivacy.Public:
                return;

            case PostPrivacy.OnlyMe:
                throw new KeyNotFoundException($"Bài đăng {post.Id} không tồn tại.");

            case PostPrivacy.Friends:
                var areFriends = await _friendRepo.AreFriendsAsync(viewerId, post.UserId);
                if (!areFriends)
                    throw new ForbiddenException("Bạn không đủ quyền xem bài viết này.");
                return;
        }
    }

    private async Task<PostResponseDto> BuildPostResponseAsync(Post post, Guid viewerId)
    {
        var list = await BuildPostResponsesAsync([post], viewerId);
        return list[0];
    }

    private async Task<List<PostResponseDto>> BuildPostResponsesAsync(
        IReadOnlyList<Post> posts, Guid viewerId)
    {
        if (posts.Count == 0) return [];

        var postIds = posts.Select(p => p.Id).ToList();

        var likes = await _likeRepo.GetByPostIdsAsync(postIds);
        var comments = await _commentRepo.GetAsync(c => postIds.Contains(c.PostId) && c.DeletedAt == null);

        var shareCounts = await _postRepo.GetShareCountsAsync(postIds);
        var sharedByMe = await _postRepo.GetSharedPostIdsByUserAsync(viewerId, postIds);

        var originalPostIds = posts
            .Where(p => p.OriginalPostId.HasValue)
            .Select(p => p.OriginalPostId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, Post> originalPosts = [];
        if (originalPostIds.Count > 0)
        {
            var originals = await _postRepo.GetOriginalPostsAsync(originalPostIds);
            originalPosts = originals.ToDictionary(p => p.Id);
        }

        var likesByPost = likes.ToLookup(l => l.PostId);
        var commentsByPost = comments.ToLookup(c => c.PostId);

        var result = new List<PostResponseDto>(posts.Count);

        foreach (var post in posts)
        {
            var dto = _mapper.Map<PostResponseDto>(post);
            dto.LikeCount = likesByPost[post.Id].Count();
            dto.CommentCount = commentsByPost[post.Id].Count();
            dto.IsLikedByMe = likesByPost[post.Id].Any(l => l.UserId == viewerId);
            dto.IsOwner = post.UserId == viewerId;
            dto.ShareCount = shareCounts.GetValueOrDefault(post.Id, 0);
            dto.IsSharedByMe = sharedByMe.Contains(post.Id);

            // GroupName không có trên entity nên set thủ công từ navigation property.
            // Group đã được include trong GetFeedPostsAsync và GetUserPostsAsync.
            if (post.Group is not null)
            {
                dto.GroupName = post.Group.Name;
            }

            if (post.OriginalPostId.HasValue &&
                originalPosts.TryGetValue(post.OriginalPostId.Value, out var orig))
            {
                dto.OriginalPost = new OriginalPostDto
                {
                    Id = orig.Id,
                    Content = orig.Content,
                    CreatedAt = orig.CreatedAt,
                    Author = _mapper.Map<UserBriefDto>(orig.User),
                    MediaFiles = _mapper.Map<List<PostMediaDto>>(orig.PostMediaFiles),
                    IsDeleted = orig.IsDeleted
                };
            }
            else if (post.OriginalPostId.HasValue)
            {
                dto.OriginalPost = new OriginalPostDto { IsDeleted = true };
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<PostResponseDto> SharePostAsync(
        Guid userId, Guid originalPostId, SharePostRequestDto dto)
    {
        var originalPost = await _postRepo.FirstOrDefaultAsync(
            p => p.Id == originalPostId && p.DeletedAt == null,
            default,
            p => p.User, p => p.PostMediaFiles);

        if (originalPost is null)
            throw new KeyNotFoundException($"Bài đăng {originalPostId} không tồn tại.");

        await EnsureViewableAsync(originalPost, userId);

        if (originalPost.OriginalPostId.HasValue)
            throw new InvalidOperationException(
                "Không thể chia sẻ lại bài viết đã là bài chia sẻ. " +
                "Hãy chia sẻ từ bài viết gốc.");

        var content = dto.Content?.Trim();
        var sharePost = new Post
        {
            UserId = userId,
            Content = string.IsNullOrWhiteSpace(content) ? null : content,
            Privacy = dto.Privacy,
            OriginalPostId = originalPostId
        };

        await _postRepo.AddAsync(sharePost);
        await _postRepo.SaveChangesAsync();

        if (userId != originalPost.UserId)
        {
            await CreateNotificationAsync(
                recipientId: originalPost.UserId,
                actorId: userId,
                type: NotificationType.Share,
                entityId: originalPostId,
                content: "đã chia sẻ bài viết của bạn.");
        }

        var savedShare = await _postRepo.FirstOrDefaultAsync(
            p => p.Id == sharePost.Id, default, p => p.User, p => p.PostMediaFiles);

        var responseList = await BuildPostResponsesAsync([savedShare!], userId);
        return responseList[0];
    }

    private sealed record PostMediaUploadResult(
        string Url, string PublicId, long Size, MediaType MediaType, StorageProvider StorageProvider);

    private void ValidatePostMedia(IFormFile file)
    {
        if (file.Length == 0)
            throw new ArgumentException("File media không được rỗng (0 byte).");

        var ct = file.ContentType?.Trim().ToLower() ?? string.Empty;

        if (_fileValidation.AllowedImageContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
        {
            if (file.Length > MaxImageBytes)
                throw new ArgumentOutOfRangeException(nameof(file),
                    $"Ảnh không được vượt quá {MaxImageBytes / 1024 / 1024} MB.");

            using var stream = file.OpenReadStream();
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            if (read < 3 || !IsValidImageMagicBytes(header[..read]))
                throw new ArgumentException("File ảnh không hợp lệ (sai định dạng thật của file).");
            return;
        }

        if (_fileValidation.AllowedVideoContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
        {
            if (file.Length > _r2Settings.MaxVideoSizeBytes)
                throw new ArgumentOutOfRangeException(nameof(file),
                    $"Video không được vượt quá {_r2Settings.MaxVideoSizeBytes / 1024 / 1024} MB.");

            using var stream = file.OpenReadStream();
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            if (read < 8 || !IsValidVideoSignature(ct, header[..read]))
                throw new ArgumentException("File video không hợp lệ (sai định dạng thật của file).");
            return;
        }

        if (_fileValidation.AllowedAudioContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
        {
            if (file.Length > _r2Settings.MaxAudioSizeBytes)
                throw new ArgumentOutOfRangeException(nameof(file),
                    $"Audio không được vượt quá {_r2Settings.MaxAudioSizeBytes / 1024 / 1024} MB.");

            using var stream = file.OpenReadStream();
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            if (read < 3 || !IsValidAudioSignature(ct, header[..read]))
                throw new ArgumentException("File audio không hợp lệ (sai định dạng thật của file).");
            return;
        }

        throw new ArgumentException(
            $"Loại file '{file.ContentType}' không được hỗ trợ (chỉ nhận ảnh/video/audio theo whitelist cấu hình).");
    }

    private bool IsValidImageMagicBytes(ReadOnlySpan<byte> header)
    {
        foreach (var hex in _fileValidation.ImageMagicBytes.Values)
        {
            byte[] magic;
            try { magic = Convert.FromHexString(hex); }
            catch (FormatException) { continue; }

            if (header.Length >= magic.Length && header[..magic.Length].SequenceEqual(magic))
                return true;
        }
        return false;
    }

    private static readonly byte[] WebmMagic = [0x1A, 0x45, 0xDF, 0xA3];

    private static bool IsValidVideoSignature(string contentType, ReadOnlySpan<byte> header)
    {
        return contentType switch
        {
            "video/mp4" or "video/quicktime" =>
                header.Length >= 8 && header[4..8].SequenceEqual("ftyp"u8),
            "video/webm" =>
                header.Length >= 4 && header[..4].SequenceEqual(WebmMagic),
            _ => false
        };
    }

    private static bool IsValidAudioSignature(string contentType, ReadOnlySpan<byte> header)
    {
        return contentType switch
        {
            "audio/ogg" => header.Length >= 4 && header[..4].SequenceEqual("OggS"u8),
            "audio/wav" => header.Length >= 12 &&
                             header[..4].SequenceEqual("RIFF"u8) &&
                             header[8..12].SequenceEqual("WAVE"u8),
            "audio/mpeg" => (header.Length >= 3 &&
                              header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3') ||
                             (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0),
            "audio/aac" => true,
            _ => false
        };
    }

    private (MediaType MediaType, StorageProvider StorageProvider) ClassifyMedia(string contentType)
    {
        if (_fileValidation.AllowedImageContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return (MediaType.Image, StorageProvider.Cloudinary);

        if (_fileValidation.AllowedVideoContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return (MediaType.Video, StorageProvider.R2);

        return (MediaType.Audio, StorageProvider.R2);
    }

    private async Task<PostMediaUploadResult> UploadPostMediaAsync(IFormFile file, Guid postId, int index)
    {
        var result = await _cloudService.UploadMediaAsync(file, $"posts/{postId}");
        return new PostMediaUploadResult(
            result.SecureUrl,
            result.PublicId,
            result.FileSize,
            result.MediaType,
            result.StorageProvider);
    }

    private async Task CreateNotificationAsync(
        Guid recipientId, Guid actorId, NotificationType type, Guid entityId, string content)
    {
        var notification = new Notification
        {
            UserId = recipientId,
            ActorId = actorId,
            Type = type,
            EntityId = entityId,
            Content = content
        };

        await _notificationRepo.AddAsync(notification);
        await _notificationRepo.SaveChangesAsync();
    }
}