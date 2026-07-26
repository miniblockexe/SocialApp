using SocialApp.API.Extensions;
using SocialApp.API.Middleware;
using SocialApp.API.Hubs;
using SocialApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;

// BUILDER

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var env = builder.Environment;

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

// DATABASE MIGRATION

if (!env.IsProduction())
{
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
}

// MIDDLEWARE PIPELINE

// 1. Global Exception Handler
app.UseGlobalExceptionHandler();

// 2. HTTPS
if (!env.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 3. Serve Angular trong wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// 4. Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SocialApp API v1");
    options.RoutePrefix = "swagger";
    options.DisplayRequestDuration();
});

// 5. CORS
app.UseCors("AllowFrontend");

// 6. Rate Limiter
app.UseRateLimiter();

// 7. Routing
app.UseRouting();

// 8. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 9. Ban Check
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

// Angular SPA Fallback
app.MapFallbackToFile("index.html");


app.Logger.LogInformation(
    "SocialApp đang chạy ở {Environment} mode. URL: {Urls}",
    env.EnvironmentName,
    string.Join(", ", app.Urls));

await app.RunAsync();