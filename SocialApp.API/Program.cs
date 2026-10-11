using SocialApp.API.Extensions;
using SocialApp.API.Middleware;
using SocialApp.API.Hubs;
using SocialApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;

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
builder.Services.AddClientRateLimiting(config);
builder.Services.AddApplicationSignalR(config);
builder.Services.AddCloudStorage(config);
builder.Services.AddGeminiAI(config);
builder.Services.AddApplicationOptions(config);
builder.Services.AddApplicationServices();
builder.Services.AddHttpClient();
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

    options.HeadContent = """
<script src="https://accounts.google.com/gsi/client" async defer></script>
<script>
window.addEventListener('load', function () {
  var inited = false;
  function mount() {
    if (!window.google) return;
    var wrap = document.querySelector('.auth-wrapper');          
    if (!wrap || document.getElementById('gbox')) return;
    var box = document.createElement('div');
    box.id = 'gbox';
    box.style = 'display:inline-flex;align-items:center;gap:8px;margin-right:12px';
    box.innerHTML = '<div id="gbtn"></div>' +
      '<input id="gtok" readonly placeholder="idToken" style="width:220px;padding:6px;font-size:12px" />' +
      '<small id="gmsg"></small>';
    wrap.insertBefore(box, wrap.firstChild);                     
    if (!inited) {
      inited = true;
      google.accounts.id.initialize({
        client_id: '181990983325-06376ui32t35lb5e3q1e1imgku9sokat.apps.googleusercontent.com',
        callback: function (r) {
          var tok = r.credential;
          document.getElementById('gtok').value = tok;
          var msg = document.getElementById('gmsg');
          if (navigator.clipboard) {
            navigator.clipboard.writeText(tok).then(
              function () { msg.textContent = 'Đã copy idToken, dán vào body google-login'; },
              function () { msg.textContent = 'Bấm vào ô rồi Ctrl+C để copy'; });
          } else { msg.textContent = 'Bấm vào ô rồi Ctrl+C để copy'; }
        }
      });
    }
    google.accounts.id.renderButton(document.getElementById('gbtn'), { theme: 'outline' });
    document.getElementById('gtok').addEventListener('focus', function () { this.select(); });
  }
  setInterval(mount, 500);                                     
});
</script>
""";
});
// 4. CORS
app.UseCors("AllowFrontend");
// 5. Routing
app.UseRouting();
// 6. Authentication
app.UseAuthentication();
// 7. Rate Limiter
app.UseRateLimiter();
// 8. Authorization
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
app.Logger.LogInformation(
    "SocialApp đang chạy ở {Environment} mode. URL: {Urls}",
    env.EnvironmentName,
    string.Join(", ", app.Urls));
await app.RunAsync();