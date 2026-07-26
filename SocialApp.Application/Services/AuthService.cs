using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Services;

/// <summary>
/// Xử lý toàn bộ business logic liên quan đến xác thực:
/// đăng ký, đăng nhập, refresh token, revoke token, đổi mật khẩu.
///
/// Tích hợp:
///  - Mailboxlayer: xác thực email tồn tại thật khi đăng ký.
///  - DiceBear:     sinh avatar mặc định theo username khi đăng ký.

/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _tokenRepo;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailVerificationService _emailVerifier;
    private readonly ILogger<AuthService> _logger;

    private const string LoginAttemptCachePrefix = "login_attempt:";
    private const int MaxLoginAttempts = 5;
    private static readonly TimeSpan LoginLockoutWindow = TimeSpan.FromMinutes(15);

    // DiceBear — avatar SVG tự động theo seed = username
    // Style avataaars phù hợp với mạng xã hội
    private const string DiceBearBaseUrl = "https://api.dicebear.com/7.x/avataaars/svg";

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository tokenRepo,
        IMapper mapper,
        IMemoryCache cache,
        IOptions<JwtSettings> jwtOptions,
        IEmailVerificationService emailVerifier,
        ILogger<AuthService> logger)
    {
        _userRepo      = userRepo;
        _tokenRepo     = tokenRepo;
        _mapper        = mapper;
        _cache         = cache;
        _jwtSettings   = jwtOptions.Value;
        _emailVerifier = emailVerifier;
        _logger        = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto)
    {
        var email    = dto.Email.Trim().ToLower();
        var username = dto.Username.Trim();
        var fullName = dto.FullName.Trim();

        // Mailboxlayer: xác thực email
        var emailCheck = await _emailVerifier.VerifyAsync(email);
        if (emailCheck is not null)
        {
            if (!emailCheck.FormatValid)
            {
                _logger.LogWarning("[Register] Email sai định dạng (Mailboxlayer): {Email}", email);
                throw new ArgumentException("EMAIL_FORMAT_INVALID");
            }

            if (emailCheck.IsDisposable)
            {
                _logger.LogWarning("[Register] Disposable email bị từ chối: {Email}", email);
                throw new ArgumentException("EMAIL_DISPOSABLE");
            }

            // smtp_check = false không nhất thiết block — nhiều server chặn ping,
            // chỉ warn để có thể review sau.
            if (!emailCheck.SmtpValid)
                _logger.LogWarning("[Register] SMTP check fail cho email: {Email} (vẫn cho đăng ký)", email);
        }

        // Check duplicate
        if (await _userRepo.EmailExistsAsync(email))
        {
            _logger.LogWarning("[Register] Email đã tồn tại: {Email}", email);
            throw new InvalidOperationException("EMAIL_EXISTED");
        }

        if (await _userRepo.UsernameExistsAsync(username))
        {
            _logger.LogWarning("[Register] Username đã tồn tại: {Username}", username);
            throw new InvalidOperationException("USERNAME_EXISTED");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12);

        // DiceBear: avatar mặc định theo seed = username
        var defaultAvatarUrl = $"{DiceBearBaseUrl}?seed={Uri.EscapeDataString(username)}&backgroundColor=b6e3f4,c0aede,d1d4f9";

        var user = new User
        {
            Id           = Guid.NewGuid(),
            Username     = username,
            Email        = email,
            PasswordHash = passwordHash,
            FullName     = fullName,
            AvatarUrl    = defaultAvatarUrl,   // ← DiceBear SVG
            Role         = UserRole.User,
            IsActive     = true,
            IsBanned     = false
        };

        var refreshToken = GenerateRefreshToken();
        refreshToken.UserId = user.Id;

        try
        {
            await _userRepo.AddAsync(user);
            await _tokenRepo.AddAsync(refreshToken);
            await _tokenRepo.SaveChangesAsync();
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "[Register] Concurrent conflict — email: {Email}", email);
            throw new InvalidOperationException("EMAIL_EXISTED");
        }

        _logger.LogInformation(
            "[Register] Thành công — UserId: {UserId}, Email: {Email}, Avatar: DiceBear, {Time:O}",
            user.Id, email, DateTime.UtcNow);

        var accessToken = GenerateAccessToken(user);
        var expiresAt   = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        return new AuthResponseDto
        {
            AccessToken  = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt    = expiresAt,
            User         = _mapper.Map<UserBriefDto>(user)
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, string ipAddress)
    {
        var email    = dto.Email.Trim().ToLower();
        var cacheKey = $"{LoginAttemptCachePrefix}{ipAddress}";

        // Rate limit: kiểm tra số lần thất bại từ IP
        if (_cache.TryGetValue(cacheKey, out LoginAttemptRecord? record) && record is not null)
        {
            if (record.Count >= MaxLoginAttempts)
            {
                var remaining        = LoginLockoutWindow - (DateTime.UtcNow - record.FirstAttemptAt);
                var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);

                _logger.LogWarning(
                    "[Login] Bị chặn — IP: {IP}, Lần thử: {Count}, Còn lại: {Seconds}s",
                    ipAddress, record.Count, remainingSeconds);

                throw new TooManyRequestsException(
                    $"Quá nhiều lần đăng nhập thất bại. Vui lòng thử lại sau {remainingSeconds} giây.");
            }
        }

        var userReadOnly = await _userRepo.GetByEmailAsync(email);

        if (userReadOnly is null)
        {
            IncrementLoginAttempt(cacheKey);
            _logger.LogWarning("[Login] Email không tồn tại: {Email}, IP: {IP}", email, ipAddress);
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, userReadOnly.PasswordHash))
        {
            IncrementLoginAttempt(cacheKey);
            _logger.LogWarning("[Login] Sai mật khẩu — UserId: {UserId}, IP: {IP}", userReadOnly.Id, ipAddress);
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");
        }

        _cache.Remove(cacheKey);

        var user = await _userRepo.FirstOrDefaultAsync(u => u.Id == userReadOnly.Id) ?? userReadOnly;

        // Revoke tất cả active token cũ
        var now          = DateTime.UtcNow;
        var activeTokens = await _tokenRepo.GetActiveTokensByUserIdAsync(user.Id);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }
        _tokenRepo.UpdateRange(activeTokens);

        // Tạo refresh token mới
        var newRefreshToken = GenerateRefreshToken();
        newRefreshToken.UserId    = user.Id;
        newRefreshToken.IpAddress = ipAddress;

        user.LastSeen = now;
        _userRepo.Update(user);

        await _tokenRepo.AddAsync(newRefreshToken);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation(
            "[Login] Thành công — UserId: {UserId}, IP: {IP}, {Time:O}",
            user.Id, ipAddress, now);

        var accessToken = GenerateAccessToken(user);
        var expiresAt   = now.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        return new AuthResponseDto
        {
            AccessToken  = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt    = expiresAt,
            User         = _mapper.Map<UserBriefDto>(user)
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedAccessException("Token không hợp lệ.");

        var storedToken = await _tokenRepo.GetByTokenAsync(refreshToken);

        if (storedToken is null)
        {
            _logger.LogWarning("[Refresh] Token không tồn tại: {Token}", MaskToken(refreshToken));
            throw new UnauthorizedAccessException("Token không hợp lệ.");
        }

        // REPLAY ATTACK: token đã revoke mà vẫn dùng lại
        if (storedToken.IsRevoked)
        {
            var utcNow      = DateTime.UtcNow;
            var allActive   = await _tokenRepo.GetNonRevokedTokensByUserIdAsync(storedToken.UserId);
            foreach (var t in allActive)
            {
                t.IsRevoked = true;
                t.RevokedAt = utcNow;
            }
            _tokenRepo.UpdateRange(allActive);
            await _tokenRepo.SaveChangesAsync();

            _logger.LogCritical(
                "⚠️ REPLAY ATTACK — UserId: {UserId}, Token: {Token}, {Time:O}",
                storedToken.UserId, MaskToken(refreshToken), utcNow);

            throw new UnauthorizedAccessException("Token đã bị thu hồi — phiên đăng nhập bị chấm dứt.");
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogInformation("[Refresh] Token hết hạn — UserId: {UserId}", storedToken.UserId);
            throw new UnauthorizedAccessException("Token đã hết hạn.");
        }

        var user = storedToken.User;

        if (user.IsBanned)
        {
            _logger.LogWarning("[Refresh] User bị ban — UserId: {UserId}", user.Id);
            throw new ForbiddenException("Tài khoản đã bị khóa.");
        }

        // Token rotation
        var now = DateTime.UtcNow;
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = now;
        _tokenRepo.Update(storedToken);

        var newRefreshToken = GenerateRefreshToken();
        newRefreshToken.UserId    = user.Id;
        // Kế thừa IP từ token cũ
        newRefreshToken.IpAddress = storedToken.IpAddress;

        await _tokenRepo.AddAsync(newRefreshToken);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation("[Refresh] Token rotation OK — UserId: {UserId}, {Time:O}", user.Id, now);

        var accessToken = GenerateAccessToken(user);
        var expiresAt   = now.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        return new AuthResponseDto
        {
            AccessToken  = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt    = expiresAt,
            User         = _mapper.Map<UserBriefDto>(user)
        };
    }

    public async Task RevokeTokenAsync(string refreshToken, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new KeyNotFoundException("Token không tồn tại.");

        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("Không xác định được danh tính người dùng.");

        var storedToken = await _tokenRepo.GetByTokenAsync(refreshToken);

        if (storedToken is null)
        {
            _logger.LogWarning("[Revoke] Token không tồn tại, UserId: {UserId}", userId);
            throw new KeyNotFoundException("Token không tồn tại.");
        }

        if (storedToken.UserId != userId)
        {
            _logger.LogWarning(
                "[Revoke] Token không thuộc về user — UserId: {UserId}, Owner: {OwnerId}",
                userId, storedToken.UserId);
            throw new ForbiddenException("Bạn không có quyền thu hồi token này.");
        }

        if (storedToken.IsRevoked)
            throw new InvalidOperationException("Token đã được thu hồi trước đó.");

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        _tokenRepo.Update(storedToken);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation("[Revoke] UserId: {UserId}, {Time:O}", userId, DateTime.UtcNow);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        if (userId == Guid.Empty)
            throw new KeyNotFoundException("Không tìm thấy người dùng.");

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("[ChangePassword] User không tồn tại: {UserId}", userId);
            throw new KeyNotFoundException("Không tìm thấy người dùng.");
        }

        if (dto.NewPassword == dto.OldPassword)
            throw new InvalidOperationException("NEW_PASSWORD_SAME_AS_OLD");

        if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
        {
            _logger.LogWarning("[ChangePassword] Sai mật khẩu cũ — UserId: {UserId}", userId);
            throw new InvalidOperationException("OLD_PASSWORD_WRONG");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        _userRepo.Update(user);

        var now       = DateTime.UtcNow;
        var allTokens = await _tokenRepo.GetNonRevokedTokensByUserIdAsync(userId);
        foreach (var token in allTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }
        _tokenRepo.UpdateRange(allTokens);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation(
            "[ChangePassword] OK — UserId: {UserId}, {Count} token revoked, {Time:O}",
            userId, allTokens.Count, now);
    }

    public string GenerateAccessToken(User user)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,  user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("preferred_username",           user.Username),
            new Claim(ClaimTypes.Role,                user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,    Guid.NewGuid().ToString())
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer:             _jwtSettings.Issuer,
            audience:           _jwtSettings.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken()
    {
        return new RefreshToken
        {
            Id        = Guid.NewGuid(),
            Token     = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };
    }

    private void IncrementLoginAttempt(string cacheKey)
    {
        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(cacheKey, out LoginAttemptRecord? existing) && existing is not null)
        {
            _cache.Set(cacheKey, existing with { Count = existing.Count + 1 }, LoginLockoutWindow);
        }
        else
        {
            _cache.Set(cacheKey, new LoginAttemptRecord(Count: 1, FirstAttemptAt: now), LoginLockoutWindow);
        }
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("23505",             StringComparison.OrdinalIgnoreCase)
            || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key",     StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length <= 12) return "***";
        return $"{token[..8]}...{token[^4..]}";
    }
}

// Internal record cho login attempt tracking
internal sealed record LoginAttemptRecord(int Count, DateTime FirstAttemptAt);
