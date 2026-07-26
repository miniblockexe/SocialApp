using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Users;
using SocialApp.Application.DTOs.Cloud;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;
using AutoMapper;

namespace SocialApp.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IFriendRequestRepository _friendRepo;
    private readonly IGenericRepository<Post> _postRepo;
    private readonly ICloudService _cloudService;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;
    private readonly FileValidationSettings _fileValidation;

    private const long AvatarMaxBytes = 5 * 1024 * 1024;
    private const long CoverMaxBytes = 10 * 1024 * 1024;

    public UserService(
        IUserRepository userRepo,
        IFriendRequestRepository friendRepo,
        IGenericRepository<Post> postRepo,
        ICloudService cloudService,
        IMapper mapper,
        ILogger<UserService> logger,
        IOptions<FileValidationSettings> fileValidationOptions)
    {
        _userRepo = userRepo;
        _friendRepo = friendRepo;
        _postRepo = postRepo;
        _cloudService = cloudService;
        _mapper = mapper;
        _logger = logger;
        _fileValidation = fileValidationOptions.Value;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid targetId, Guid viewerId)
    {
        if (targetId == Guid.Empty) throw new ArgumentException("targetId không hợp lệ.");
        var user = await _userRepo.GetByIdAsync(targetId)
            ?? throw new KeyNotFoundException($"Người dùng {targetId} không tồn tại.");
        var dto = _mapper.Map<UserProfileDto>(user);
        dto.FriendCount = await _friendRepo.CountFriendsAsync(targetId);
        dto.PostCount = await _postRepo.CountAsync(p => p.UserId == targetId);
        dto.FriendshipStatus = await ComputeFriendshipStatusAsync(viewerId, targetId);
        return dto;
    }

    public async Task<UserProfileDto> GetMyProfileAsync(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("userId không hợp lệ.");
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"Người dùng {userId} không tồn tại.");
        var dto = _mapper.Map<UserProfileDto>(user);
        dto.FriendCount = await _friendRepo.CountFriendsAsync(userId);
        dto.PostCount = await _postRepo.CountAsync(p => p.UserId == userId);
        dto.FriendshipStatus = FriendshipStatus.None;
        return dto;
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        if (userId == Guid.Empty) throw new ArgumentException("userId không hợp lệ.");
        var user = await _userRepo.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException($"Người dùng {userId} không tồn tại.");
        var updated = false;
        if (dto.FullName is not null)
        {
            var trimmed = dto.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed)) { user.FullName = trimmed; updated = true; }
        }
        if (dto.Bio is not null)
        {
            user.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();
            updated = true;
        }
        if (updated) { _userRepo.Update(user); await _userRepo.SaveChangesAsync(); }
        return await GetMyProfileAsync(userId);
    }

    public async Task<string> UpdateAvatarAsync(Guid userId, IFormFile file)
    {
        if (userId == Guid.Empty) throw new ArgumentException("userId không hợp lệ.");
        ValidateImageFile(file, AvatarMaxBytes, "Avatar");

        var user = await _userRepo.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException($"Người dùng {userId} không tồn tại.");

        // Xóa avatar cũ trên Cloudinary dùng PublicId (best-effort, fire-and-forget)
        // KHÔNG dùng AvatarUrl vì Cloudinary.DeleteAsync cần PublicId, không phải URL đầy đủ
        if (!string.IsNullOrWhiteSpace(user.AvatarPublicId))
            _ = _cloudService.DeleteMediaAsync(
                user.AvatarPublicId, StorageProvider.Cloudinary, MediaType.Image);

        var result = await _cloudService.UploadMediaAsync(file, "avatars");

        // Lưu cả URL (để hiển thị) và PublicId (để xóa sau này)
        user.AvatarUrl = result.SecureUrl;
        user.AvatarPublicId = result.PublicId;
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync();

        _logger.LogInformation(
            "[UserService] Avatar updated — UserId: {Id}, PublicId: {PublicId}",
            userId, result.PublicId);
        return result.SecureUrl;
    }

    public async Task<string> UpdateCoverAsync(Guid userId, IFormFile file)
    {
        if (userId == Guid.Empty) throw new ArgumentException("userId không hợp lệ.");
        ValidateImageFile(file, CoverMaxBytes, "Ảnh bìa");

        var user = await _userRepo.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException($"Người dùng {userId} không tồn tại.");

        // Xóa cover cũ trên Cloudinary dùng PublicId (best-effort, fire-and-forget)
        if (!string.IsNullOrWhiteSpace(user.CoverPublicId))
            _ = _cloudService.DeleteMediaAsync(
                user.CoverPublicId, StorageProvider.Cloudinary, MediaType.Image);

        var result = await _cloudService.UploadMediaAsync(file, "covers");

        // Lưu cả URL và PublicId
        user.CoverPhotoUrl = result.SecureUrl;
        user.CoverPublicId = result.PublicId;
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync();

        _logger.LogInformation(
            "[UserService] Cover updated — UserId: {Id}, PublicId: {PublicId}",
            userId, result.PublicId);
        return result.SecureUrl;
    }

    public async Task<PagedResult<UserSearchResultDto>> SearchUsersAsync(
        Guid viewerId, string keyword, int page, int size)
    {
        if (viewerId == Guid.Empty) throw new ArgumentException("viewerId không hợp lệ.");
        keyword = keyword?.Trim() ?? string.Empty;
        if (keyword.Length < 2) throw new ArgumentException("Từ khóa phải có ít nhất 2 ký tự.");
        page = page < 1 ? 1 : page;
        size = size < 1 ? 10 : size > 100 ? 100 : size;
        var keywordLower = keyword.ToLower();
        var blockedIds = await _friendRepo.GetBlockedUserIdsAsync(viewerId);
        var allMatching = await _userRepo.GetAsync(u =>
            u.Id != viewerId && !blockedIds.Contains(u.Id) &&
            (u.Username.ToLower().Contains(keywordLower) ||
             u.FullName.ToLower().Contains(keywordLower)));
        var totalCount = allMatching.Count;
        var withMutual = new List<(User User, int MutualCount)>();
        foreach (var u in allMatching)
        {
            var mutual = await _friendRepo.CountMutualFriendsAsync(viewerId, u.Id);
            withMutual.Add((u, mutual));
        }
        var sorted = withMutual
            .OrderByDescending(x => x.MutualCount).ThenBy(x => x.User.FullName)
            .Skip((page - 1) * size).Take(size).ToList();
        var items = new List<UserSearchResultDto>();
        foreach (var (user, mutualCount) in sorted)
        {
            var req = await _friendRepo.GetBetweenUsersAsync(viewerId, user.Id);
            var status = ComputeFriendshipStatusFromRequest(viewerId, req);
            var d = _mapper.Map<UserSearchResultDto>(user);
            d.MutualFriendsCount = mutualCount;
            d.FriendshipStatus = status;
            items.Add(d);
        }
        return PagedResult<UserSearchResultDto>.Create(items, totalCount, page, size);
    }

    public async Task<UserBriefDto> GetUserBriefAsync(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("userId không hợp lệ.");
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"Người dùng {userId} không tồn tại.");
        return _mapper.Map<UserBriefDto>(user);
    }

    private async Task<FriendshipStatus> ComputeFriendshipStatusAsync(Guid viewerId, Guid targetId)
    {
        if (viewerId == targetId) return FriendshipStatus.None;
        var req = await _friendRepo.GetBetweenUsersAsync(viewerId, targetId);
        return ComputeFriendshipStatusFromRequest(viewerId, req);
    }

    private static FriendshipStatus ComputeFriendshipStatusFromRequest(
        Guid viewerId, FriendRequest? req)
    {
        if (req is null) return FriendshipStatus.None;
        return req.Status switch
        {
            FriendStatus.Accepted => FriendshipStatus.Friends,
            FriendStatus.Blocked => FriendshipStatus.Blocked,
            FriendStatus.Pending when req.SenderId == viewerId => FriendshipStatus.SentRequest,
            FriendStatus.Pending => FriendshipStatus.Pending,
            _ => FriendshipStatus.None
        };
    }

    private void ValidateImageFile(IFormFile file, long maxBytes, string fieldName)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException($"{fieldName} không được để trống.");
        if (file.Length > maxBytes)
            throw new ArgumentOutOfRangeException(nameof(file),
                $"{fieldName} không được vượt quá {maxBytes / 1024 / 1024} MB.");
        var ct = file.ContentType?.Trim().ToLower() ?? string.Empty;
        if (!_fileValidation.AllowedImageContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"{fieldName} chỉ chấp nhận JPEG, PNG, GIF, WEBP.");
        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        if (read < 3) throw new ArgumentException($"{fieldName} không hợp lệ (file quá nhỏ).");
        if (!IsValidImageMagicBytes(header[..read]))
            throw new ArgumentException($"{fieldName} không phải ảnh hợp lệ.");
    }

    private bool IsValidImageMagicBytes(ReadOnlySpan<byte> header)
    {
        foreach (var hex in _fileValidation.ImageMagicBytes.Values)
        {
            byte[] magic;
            try { magic = Convert.FromHexString(hex); }
            catch (FormatException) { continue; } // config sai định dạng — bỏ qua, không crash request

            if (header.Length >= magic.Length && header[..magic.Length].SequenceEqual(magic))
                return true;
        }
        return false;
    }
}