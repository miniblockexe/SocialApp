using SocialApp.API.Extensions;
using SocialApp.API.Middleware;
using SocialApp.API.Hubs;
using SocialApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;

// BUILDER
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("/etc/secrets/appsettings.Production.json", optional: true, reloadOnChange: false);

var config = builder.Configuration;
var env = builder.Environment;

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = 15L * 1024 * 1024; // 15 MB
});

builder.Services.Configure<FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = 15L * 1024 * 1024; // 15 MB
    opt.ValueLengthLimit = 4 * 1024 * 1024;   // 4 MB 
    opt.KeyLengthLimit = 2048;
});

// Controllers + JSON
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Application layers
builder.Services.AddDatabase(config);
builder.Services.AddJwtAuthentication(config);
builder.Services.AddApplicationCors(config);
builder.Services.AddApplicationRateLimiting(config);
builder.Services.AddApplicationSignalR(config);
builder.Services.AddCloudStorage(config);
builder.Services.AddGeminiAI(config);
builder.Services.AddApplicationOptions(config);
builder.Services.AddApplicationServices();
builder.Services.AddSwaggerWithJwt();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

// DATABASE MIGRATION
using var scope = app.Services.CreateScope();
try
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database migration hoàn thành.");
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Migration thất bại — ứng dụng sẽ không khởi động.");
    throw;
}

// MIDDLEWARE PIPELINE
// 1. Global Exception Handler
app.UseGlobalExceptionHandler();

// 2. HTTPS
if (!env.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 3. Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SocialApp API v1");
    options.RoutePrefix = "swagger";
    options.DisplayRequestDuration();
});

// 4. CORS
app.UseCors("AllowFrontend");

// 5. Rate Limiter
app.UseRateLimiter();

// 6. Routing
app.UseRouting();

// 7. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 8. Ban Check
app.UseBannedUserCheck();

// ENDPOINTS
// Health Check
app.MapHealthChecks("/health");

// Controllers (API)
app.MapControllers();

// SignalR
var signalRConfig = config.GetSection("SignalRSettings");
app.MapHub<ChatHub>(
    signalRConfig["ChatHubPath"] ?? "/hubs/chat");
app.MapHub<NotificationHub>(
    signalRConfig["NotificationHubPath"] ?? "/hubs/notification");

app.Logger.LogInformation(
    "SocialApp đang chạy ở {Environment} mode. URL: {Urls}",
    env.EnvironmentName,
    string.Join(", ", app.Urls));

await app.RunAsync();