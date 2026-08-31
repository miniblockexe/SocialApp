using System;
using System.Text;
using System.Threading.RateLimiting;
using Amazon.S3;
using CloudinaryDotNet;
using FluentValidation;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SocialApp.API.Middleware;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Infrastructure.Data;
using SocialApp.Infrastructure.Services;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using ApplicationJwtSettings = SocialApp.Application.Settings.JwtSettings;
using ApplicationFileValidationSettings = SocialApp.Application.Settings.FileValidationSettings;
using ApplicationCloudflareR2Settings = SocialApp.Application.Settings.CloudflareR2Settings;
using SocialApp.Application.Interfaces.Repositories;
using ApplicationCloudinarySettings = SocialApp.Application.Settings.CloudinarySettings;
using IAdminDbContextAlias = SocialApp.Application.Interfaces.Repositories.IAdminDbContext;

namespace SocialApp.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);

                    npgsql.CommandTimeout(30);
                    npgsql.MigrationsAssembly("SocialApp.Infrastructure");
                });

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        var jwtSection = config.GetSection("JwtSettings");
        var secretKey = jwtSection["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");

        if (secretKey.Length < 32)
            throw new InvalidOperationException("JwtSettings:SecretKey phải có ít nhất 32 ký tự.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidAudience = jwtSection["Audience"],
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;

                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            (path.StartsWithSegments("/hubs/chat") ||
                             path.StartsWithSegments("/hubs/notification")))
                        {
                            ctx.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddApplicationCors(
        this IServiceCollection services,
        IConfiguration config)
    {
        var allowedOrigins = config
            .GetSection("CorsSettings:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddApplicationRateLimiting(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("login", o =>
            {
                o.PermitLimit = config.GetValue("RateLimitSettings:Login:PermitLimit", 5);
                o.Window = TimeSpan.FromSeconds(config.GetValue("RateLimitSettings:Login:WindowSeconds", 900));
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("register", o =>
            {
                o.PermitLimit = config.GetValue("RateLimitSettings:Register:PermitLimit", 3);
                o.Window = TimeSpan.FromSeconds(config.GetValue("RateLimitSettings:Register:WindowSeconds", 3600));
                o.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("upload", o =>
            {
                o.PermitLimit = config.GetValue("RateLimitSettings:Upload:PermitLimit", 20);
                o.Window = TimeSpan.FromSeconds(config.GetValue("RateLimitSettings:Upload:WindowSeconds", 60));
                o.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("gemini", o =>
            {
                o.PermitLimit = config.GetValue("RateLimitSettings:GeminiAI:PermitLimit", 10);
                o.Window = TimeSpan.FromSeconds(config.GetValue("RateLimitSettings:GeminiAI:WindowSeconds", 60));
                o.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("default", o =>
            {
                o.PermitLimit = config.GetValue("RateLimitSettings:Default:PermitLimit", 100);
                o.Window = TimeSpan.FromSeconds(config.GetValue("RateLimitSettings:Default:WindowSeconds", 60));
                o.QueueLimit = 0;
            });
        });

        return services;
    }

    public static IServiceCollection AddApplicationSignalR(
        this IServiceCollection services,
        IConfiguration config)
    {
        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors =
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                options.MaximumParallelInvocationsPerClient =
                    config.GetValue("SignalRSettings:MaximumParallelInvocationsPerClient", 1);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase;
            });

        return services;
    }

    public static IServiceCollection AddCloudStorage(
      this IServiceCollection services,
      IConfiguration config)
    {
        var cloudinarySection = config.GetSection("CloudinarySettings");
        var cloudinary = new Cloudinary(new Account(
            cloudinarySection["CloudName"]
                ?? throw new InvalidOperationException("CloudinarySettings:CloudName chưa cấu hình."),
            cloudinarySection["ApiKey"]
                ?? throw new InvalidOperationException("CloudinarySettings:ApiKey chưa cấu hình."),
            cloudinarySection["ApiSecret"]
                ?? throw new InvalidOperationException("CloudinarySettings:ApiSecret chưa cấu hình.")
        ));
        cloudinary.Api.Secure = true;
        services.AddSingleton(cloudinary);

        services.AddHttpClient("Cloudinary", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.Configure<ApplicationCloudinarySettings>(
            config.GetSection("CloudinarySettings"));

        return services;
    }

    public static IServiceCollection AddGeminiAI(
        this IServiceCollection services,
        IConfiguration config)
    {
        var geminiSection = config.GetSection("GeminiSettings");
        var baseUrl = geminiSection["BaseUrl"]
            ?? "https://generativelanguage.googleapis.com/v1beta/models";

        services.AddHttpClient("Gemini", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<ApplicationJwtSettings>(config.GetSection("JwtSettings"));
        services.Configure<ApplicationCloudinarySettings>(config.GetSection("CloudinarySettings"));
        services.Configure<ApplicationCloudflareR2Settings>(config.GetSection("CloudflareR2Settings"));
        services.Configure<GeminiSettings>(config.GetSection("GeminiSettings"));
        services.Configure<RateLimitSettings>(config.GetSection("RateLimitSettings"));
        services.Configure<ApplicationFileValidationSettings>(config.GetSection("FileValidationSettings"));
        services.Configure<PaginationSettings>(config.GetSection("PaginationSettings"));

        services.Configure<SocialApp.Application.Settings.MailboxlayerSettings>(config.GetSection("MailboxlayerSettings"));
        services.Configure<SocialApp.Application.Settings.TinyUrlSettings>(config.GetSection("TinyUrlSettings"));
        services.Configure<SocialApp.Application.Settings.TenorSettings>(config.GetSection("TenorSettings"));

        // ── MỚI: Gmail + Google OAuth settings ───────────────────────────────
        services.Configure<GmailSettings>(config.GetSection("GmailSettings"));
        services.Configure<GoogleSettings>(config.GetSection("GoogleSettings"));

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddAutoMapper(typeof(SocialApp.Application.AssemblyMarker).Assembly);

        services.AddValidatorsFromAssembly(
            typeof(SocialApp.Application.AssemblyMarker).Assembly);

        services.AddScoped<IBanStatusChecker,
            SocialApp.Infrastructure.Services.BanStatusChecker>();

        // Repositories
        services.AddScoped(typeof(IGenericRepository<>),
            typeof(SocialApp.Infrastructure.Repositories.GenericRepository<>));
        services.AddScoped<SocialApp.Application.Interfaces.Repositories.IPostRepository,
            SocialApp.Infrastructure.Repositories.PostRepository>();
        services.AddScoped<IUserRepository,
            SocialApp.Infrastructure.Repositories.UserRepository>();
        services.AddScoped<IRefreshTokenRepository,
            SocialApp.Infrastructure.Repositories.RefreshTokenRepository>();
        services.AddScoped<IFriendRequestRepository,
            SocialApp.Infrastructure.Repositories.FriendRequestRepository>();
        services.AddScoped<ILikeRepository,
            SocialApp.Infrastructure.Repositories.LikeRepository>();
        services.AddScoped<INotificationRepository,
            SocialApp.Infrastructure.Repositories.NotificationRepository>();

        // ── MỚI ──────────────────────────────────────────────────────────────
        services.AddScoped<IPasswordResetRepository,
            SocialApp.Infrastructure.Repositories.PasswordResetRepository>();

        // Services
        services.AddScoped<IAuthService,
            SocialApp.Application.Services.AuthService>();
        services.AddSingleton<ICloudinaryService,
            SocialApp.Infrastructure.Services.CloudinaryService>();
        services.AddSingleton<IR2Service,
            SocialApp.Infrastructure.Services.R2Service>();
        services.AddScoped<ICloudService,
            SocialApp.Infrastructure.Services.CloudService>();
        services.AddScoped<IUserService,
            SocialApp.Application.Services.UserService>();
        services.AddScoped<IPostService,
            SocialApp.Application.Services.PostService>();
        services.AddScoped<IFriendService,
            SocialApp.Application.Services.FriendService>();
        services.AddScoped<INotificationService,
            SocialApp.Application.Services.NotificationService>();
        services.AddScoped<IMessageDbContext>(sp =>
            sp.GetRequiredService<SocialApp.Infrastructure.Data.AppDbContext>());
        services.AddScoped<IAdminDbContext>(sp =>
            sp.GetRequiredService<SocialApp.Infrastructure.Data.AppDbContext>());
        services.AddScoped<IMessageService,
            SocialApp.Application.Services.MessageService>();
        services.AddScoped<INotificationHub,
            SocialApp.API.Services.NotificationHubService>();
        services.AddScoped<IChatHub,
            SocialApp.API.Services.ChatHubService>();
        services.AddScoped<IGeminiService,
            SocialApp.Application.Services.GeminiService>();
        services.AddScoped<IAdminService,
            SocialApp.Application.Services.AdminService>();
        services.AddScoped<IGroupRepository,
            SocialApp.Infrastructure.Repositories.GroupRepository>();
        services.AddScoped<IGroupService,
            SocialApp.Application.Services.GroupService>();

        // External API Services
        services.AddHttpClient<SocialApp.Application.Interfaces.Services.IEmailVerificationService,
            SocialApp.Infrastructure.Services.MailboxlayerService>(client =>
            { client.Timeout = TimeSpan.FromSeconds(10); });

        services.AddHttpClient<SocialApp.Application.Interfaces.Services.IProfanityFilterService,
            SocialApp.Infrastructure.Services.PurgoMalumService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "SocialApp/1.0");
            });

        services.AddHttpClient<SocialApp.Application.Interfaces.Services.IUrlShortenerService,
            SocialApp.Infrastructure.Services.TinyUrlService>(client =>
            { client.Timeout = TimeSpan.FromSeconds(8); });

        services.AddHttpClient<SocialApp.Application.Interfaces.Services.ITenorService,
            SocialApp.Infrastructure.Services.TenorService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

        services.AddHttpClient<SocialApp.Application.Interfaces.Services.IEmojiService,
            SocialApp.Infrastructure.Services.EmojiHubService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

        // ── MỚI: Google Auth + Gmail Email ───────────────────────────────────
        services.AddHttpClient<IGoogleAuthService,
            SocialApp.Infrastructure.Services.GoogleAuthService>(client =>
            { client.Timeout = TimeSpan.FromSeconds(10); });

        services.AddScoped<IEmailService,
            SocialApp.Infrastructure.Services.GmailEmailService>();

        return services;
    }

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SocialApp API",
                Version = "v1",
                Description = "Facebook clone — ASP.NET Core 8 Web API"
            });

            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhập JWT token. Ví dụ: Bearer {token}"
            };

            options.AddSecurityDefinition("Bearer", jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id   = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
            options.MapType<IFormFile>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "binary",
                Description = "File upload (multipart/form-data)"
            });
        });

        return services;
    }
}

// Strongly-typed settings classes
public sealed class JwtSettings
{
    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}

public sealed class CloudflareR2Settings
{
    public string AccountId { get; init; } = string.Empty;
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string PublicUrl { get; init; } = string.Empty;
    public string VideoFolder { get; init; } = "videos";
    public string AudioFolder { get; init; } = "audio";
    public long MaxVideoSizeBytes { get; init; } = 524_288_000;
    public long MaxAudioSizeBytes { get; init; } = 52_428_800;
}

public sealed class RateLimitPolicySettings
{
    public int PermitLimit { get; init; }
    public int WindowSeconds { get; init; }
}

public sealed class RateLimitSettings
{
    public RateLimitPolicySettings Login { get; init; } = new();
    public RateLimitPolicySettings Register { get; init; } = new();
    public RateLimitPolicySettings Upload { get; init; } = new();
    public RateLimitPolicySettings GeminiAI { get; init; } = new();
    public RateLimitPolicySettings Default { get; init; } = new();
}

public sealed class FileValidationSettings
{
    public string[] AllowedImageContentTypes { get; init; } = [];
    public string[] AllowedVideoContentTypes { get; init; } = [];
    public string[] AllowedAudioContentTypes { get; init; } = [];
    public Dictionary<string, string> ImageMagicBytes { get; init; } = [];
}

public sealed class PaginationSettings
{
    public int DefaultPageSize { get; init; } = 10;
    public int MaxPageSize { get; init; } = 100;
}