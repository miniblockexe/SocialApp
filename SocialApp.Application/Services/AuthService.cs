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

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _tokenRepo;
    private readonly IPasswordResetRepository _resetRepo;
    private readonly IEmailService _emailService;
    private readonly IGoogleAuthService _googleAuth;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailVerificationService _emailVerifier;
    private readonly ILogger<AuthService> _logger;

    private const string LoginAttemptCachePrefix = "login_attempt:";
    private const int MaxLoginAttempts = 5;
    private static readonly TimeSpan LoginLockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan VerifyTokenTtl = TimeSpan.FromMinutes(5);

    private const string DiceBearBaseUrl = "https://api.dicebear.com/7.x/avataaars/svg";

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository tokenRepo,
        IPasswordResetRepository resetRepo,
        IEmailService emailService,
        IGoogleAuthService googleAuth,
        IMapper mapper,
        IMemoryCache cache,
        IOptions<JwtSettings> jwtOptions,
        IEmailVerificationService emailVerifier,
        ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _tokenRepo = tokenRepo;
        _resetRepo = resetRepo;
        _emailService = emailService;
        _googleAuth = googleAuth;
        _mapper = mapper;
        _cache = cache;
        _jwtSettings = jwtOptions.Value;
        _emailVerifier = emailVerifier;
        _logger = logger;
    }

    // ── RegisterAsync ─────────────────────────────────────────────────────────

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        var username = dto.Username.Trim();
        var fullName = dto.FullName.Trim();

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

            if (!emailCheck.SmtpValid)
                _logger.LogWarning("[Register] SMTP check fail cho email: {Email} (vẫn cho đăng ký)", email);
        }

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

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12);
        var defaultAvatarUrl = $"{DiceBearBaseUrl}?seed={Uri.EscapeDataString(username)}&backgroundColor=b6e3f4,c0aede,d1d4f9";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            AvatarUrl = defaultAvatarUrl,
            Role = UserRole.User,
            IsActive = true,
            IsBanned = false
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
            "[Register] Thành công — UserId: {UserId}, Email: {Email}, {Time:O}",
            user.Id, email, DateTime.UtcNow);

        return BuildAuthResponse(user, refreshToken);
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, string ipAddress)
    {
        var email = dto.Email.Trim().ToLower();
        var cacheKey = $"{LoginAttemptCachePrefix}{ipAddress}";

        if (_cache.TryGetValue(cacheKey, out LoginAttemptRecord? record) && record is not null)
        {
            if (record.Count >= MaxLoginAttempts)
            {
                var remaining = LoginLockoutWindow - (DateTime.UtcNow - record.FirstAttemptAt);
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

        var now = DateTime.UtcNow;
        var activeTokens = await _tokenRepo.GetActiveTokensByUserIdAsync(user.Id);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }
        _tokenRepo.UpdateRange(activeTokens);

        var newRefreshToken = GenerateRefreshToken();
        newRefreshToken.UserId = user.Id;
        newRefreshToken.IpAddress = ipAddress;

        user.LastSeen = now;
        _userRepo.Update(user);

        await _tokenRepo.AddAsync(newRefreshToken);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation(
            "[Login] Thành công — UserId: {UserId}, IP: {IP}, {Time:O}",
            user.Id, ipAddress, now);

        return BuildAuthResponse(user, newRefreshToken);
    }

    // ── GoogleLoginAsync ──────────────────────────────────────────────────────

    public async Task<AuthResponseDto> GoogleLoginAsync(string idToken, string ipAddress)
    {
        var info = await _googleAuth.VerifyIdTokenAsync(idToken)
            ?? throw new UnauthorizedAccessException("Google token không hợp lệ.");

        if (!info.EmailVerified)
            throw new UnauthorizedAccessException("Email Google chưa được xác thực.");

        var email = info.Email.Trim().ToLower();
        var user = await _userRepo.GetByEmailAsync(email);

        if (user is null)
        {
            // Auto-register: tạo tài khoản mới từ thông tin Google
            var baseUsername = email.Split('@')[0].ToLower()
                .Replace(".", "_").Replace("+", "_");
            var username = baseUsername;
            var suffix = 1;
            while (await _userRepo.UsernameExistsAsync(username))
                username = $"{baseUsername}{suffix++}";

            user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                FullName = info.Name,
                PasswordHash = string.Empty, // Google user không có password
                AvatarUrl = info.Picture
                    ?? $"{DiceBearBaseUrl}?seed={Uri.EscapeDataString(username)}&backgroundColor=b6e3f4,c0aede,d1d4f9",
                Role = UserRole.User,
                IsActive = true,
                IsBanned = false
            };

            var rt0 = GenerateRefreshToken();
            rt0.UserId = user.Id;
            rt0.IpAddress = ipAddress;

            await _userRepo.AddAsync(user);
            await _tokenRepo.AddAsync(rt0);
            await _tokenRepo.SaveChangesAsync();

            _logger.LogInformation(
                "[GoogleLogin] Tạo tài khoản mới — UserId: {UserId}, Email: {Email}",
                user.Id, email);

            return BuildAuthResponse(user, rt0);
        }

        if (user.IsBanned)
            throw new ForbiddenException("Tài khoản đã bị khóa.");

        // Revoke active token cũ
        var now = DateTime.UtcNow;
        var active = await _tokenRepo.GetActiveTokensByUserIdAsync(user.Id);
        foreach (var t in active) { t.IsRevoked = true; t.RevokedAt = now; }
        _tokenRepo.UpdateRange(active);

        var newToken = GenerateRefreshToken();
        newToken.UserId = user.Id;
        newToken.IpAddress = ipAddress;

        user.LastSeen = now;
        _userRepo.Update(user);

        await _tokenRepo.AddAsync(newToken);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation(
            "[GoogleLogin] Đăng nhập thành công — UserId: {UserId}", user.Id);

        return BuildAuthResponse(user, newToken);
    }

    // ── ForgotPasswordAsync ───────────────────────────────────────────────────

    public async Task ForgotPasswordAsync(string email)
    {
        var normalised = email.Trim().ToLower();
        var user = await _userRepo.GetByEmailAsync(normalised);

        if (user is null) return;

        await _resetRepo.DeleteAllForUserAsync(user.Id);

        var token = new PasswordResetToken
        {
            UserId = user.Id,
            Token = GenerateOtp(),
            ExpiresAt = DateTime.UtcNow.Add(OtpTtl)
        };

        await _resetRepo.AddAsync(token);
        await _resetRepo.SaveChangesAsync();

        await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, token.Token);

        _logger.LogInformation("[ForgotPassword] OTP gửi → UserId: {UserId}", user.Id);
    }

    public async Task<VerifyOtpResponseDto> VerifyOtpAsync(string email, string otp)
    {
        var normalised = email.Trim().ToLower();
        var storedToken = await _resetRepo.GetActiveTokenAsync(normalised, otp.Trim());

        if (storedToken is null)
            throw new InvalidOperationException("OTP không hợp lệ hoặc đã hết hạn.");

        storedToken.IsUsed = true;

        var verifyToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        storedToken.VerifyToken = verifyToken;
        storedToken.VerifyTokenExpiresAt = DateTime.UtcNow.Add(VerifyTokenTtl);

        await _resetRepo.SaveChangesAsync();

        _logger.LogInformation("[VerifyOtp] OK → UserId: {UserId}", storedToken.UserId);

        return new VerifyOtpResponseDto { VerifyToken = verifyToken };
    }

    // ── ResetPasswordAsync ────────────────────────────────────────────────────

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmNewPassword)
            throw new ArgumentException("Mật khẩu xác nhận không khớp.");

        var normalised = dto.Email.Trim().ToLower();
        var storedToken = await _resetRepo.GetByVerifyTokenAsync(normalised, dto.VerifyToken.Trim());

        if (storedToken is null)
            throw new InvalidOperationException("Phiên đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng xin OTP mới.");

        var user = storedToken.User
            ?? throw new KeyNotFoundException("Tài khoản không tồn tại.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        storedToken.IsCompleted = true;
        _userRepo.Update(user);

        var now = DateTime.UtcNow;
        var allTokens = await _tokenRepo.GetNonRevokedTokensByUserIdAsync(user.Id);
        foreach (var t in allTokens) { t.IsRevoked = true; t.RevokedAt = now; }
        _tokenRepo.UpdateRange(allTokens);

        await _resetRepo.SaveChangesAsync();

        _logger.LogInformation("[ResetPassword] OK → UserId: {UserId}", user.Id);
    }

    // ── RefreshTokenAsync ─────────────────────────────────────────────────────

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

        if (storedToken.IsRevoked)
        {
            var utcNow = DateTime.UtcNow;
            var allActive = await _tokenRepo.GetNonRevokedTokensByUserIdAsync(storedToken.UserId);
            foreach (var t in allActive) { t.IsRevoked = true; t.RevokedAt = utcNow; }
            _tokenRepo.UpdateRange(allActive);
            await _tokenRepo.SaveChangesAsync();

            _logger.LogCritical(
                "REPLAY ATTACK — UserId: {UserId}, Token: {Token}, {Time:O}",
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

        var now = DateTime.UtcNow;
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = now;
        _tokenRepo.Update(storedToken);

        var newRefreshToken = GenerateRefreshToken();
        newRefreshToken.UserId = user.Id;
        newRefreshToken.IpAddress = storedToken.IpAddress;

        await _tokenRepo.AddAsync(newRefreshToken);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation("[Refresh] Token rotation OK — UserId: {UserId}, {Time:O}", user.Id, now);

        return BuildAuthResponse(user, newRefreshToken);
    }

    // ── RevokeTokenAsync ──────────────────────────────────────────────────────

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

    // ── ChangePasswordAsync ───────────────────────────────────────────────────

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

        var now = DateTime.UtcNow;
        var allTokens = await _tokenRepo.GetNonRevokedTokensByUserIdAsync(userId);
        foreach (var token in allTokens) { token.IsRevoked = true; token.RevokedAt = now; }
        _tokenRepo.UpdateRange(allTokens);
        await _tokenRepo.SaveChangesAsync();

        _logger.LogInformation(
            "[ChangePassword] OK — UserId: {UserId}, {Count} token revoked, {Time:O}",
            userId, allTokens.Count, now);
    }

    // ── Token helpers ─────────────────────────────────────────────────────────

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("preferred_username",           user.Username),
            new Claim(ClaimTypes.Role,                user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,    Guid.NewGuid().ToString())
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken()
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private AuthResponseDto BuildAuthResponse(User user, RefreshToken rt)
    {
        var accessToken = GenerateAccessToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rt.Token,
            ExpiresAt = expiresAt,
            User = _mapper.Map<UserBriefDto>(user)
        };
    }

    private static string GenerateOtp()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var num = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
        return num.ToString("D6");
    }

    private void IncrementLoginAttempt(string cacheKey)
    {
        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(cacheKey, out LoginAttemptRecord? existing) && existing is not null)
            _cache.Set(cacheKey, existing with { Count = existing.Count + 1 }, LoginLockoutWindow);
        else
            _cache.Set(cacheKey, new LoginAttemptRecord(Count: 1, FirstAttemptAt: now), LoginLockoutWindow);
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("23505", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length <= 12) return "***";
        return $"{token[..8]}...{token[^4..]}";
    }
}

internal sealed record LoginAttemptRecord(int Count, DateTime FirstAttemptAt);