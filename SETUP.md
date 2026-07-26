# SocialApp — Hướng dẫn Setup

> **Dự án:** SocialApp — Mạng xã hội (Facebook clone)  
> **Tech stack:** .NET 8 · ASP.NET Core · EF Core · PostgreSQL · SignalR · Angular · Cloudinary · Cloudflare R2 · Gemini AI

---

## Yêu cầu hệ thống

| Công cụ | Phiên bản tối thiểu | Ghi chú |
|---------|-------------------|---------|
| .NET SDK | 8.0+ | [download](https://dotnet.microsoft.com/download) |
| PostgreSQL | 15+ | Port mặc định 5432 |
| Node.js | 18+ | Cho Angular frontend |
| Angular CLI | 17+ | `npm install -g @angular/cli` |
| Git | bất kỳ | |

---

## Backend Setup

### 1. Clone repo

```bash
git clone <repo-url>
cd SocialApp
```

### 2. Cấu hình appsettings

```bash
# Copy file mẫu
cp SocialApp.API/appsettings.json SocialApp.API/appsettings.Development.json
```

Mở `appsettings.Development.json` và điền các giá trị sau:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=SocialAppDb;Username=postgres;Password=YOUR_PASSWORD"
  },

  "JwtSettings": {
    "SecretKey": "REPLACE_WITH_MIN_32_CHAR_SECRET_KEY_HERE_123456",
    "Issuer": "SocialApp.API",
    "Audience": "SocialApp.Client",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },

  "CloudinarySettings": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-cloudinary-api-key",
    "ApiSecret": "your-cloudinary-api-secret",
    "AvatarFolder": "socialapp/avatars",
    "PostImageFolder": "socialapp/posts",
    "MaxImageSizeBytes": 10485760
  },

  "CloudflareR2Settings": {
    "AccountId": "your-r2-account-id",
    "AccessKeyId": "your-r2-access-key-id",
    "SecretAccessKey": "your-r2-secret-access-key",
    "BucketName": "socialapp-media",
    "PublicUrl": "https://your-custom-domain.r2.dev",
    "VideoFolder": "videos",
    "AudioFolder": "audio",
    "MaxVideoSizeBytes": 524288000,
    "MaxAudioSizeBytes": 52428800
  },

  "GeminiSettings": {
    "ApiKey": "your-gemini-api-key",
    "Model": "gemini-1.5-flash",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/models",
    "MaxOutputTokens": 1000,
    "Temperature": 0.7,
    "MaxHistoryMessages": 20,
    "TimeoutSeconds": 30,
    "SystemPrompt": "Bạn là trợ lý AI thân thiện trong ứng dụng mạng xã hội SocialApp. Hãy trả lời ngắn gọn, hữu ích và phù hợp với ngữ cảnh trò chuyện."
  },

  "CorsSettings": {
    "AllowedOrigins": [
      "http://localhost:4200",
      "http://localhost:3000"
    ]
  }
}
```

> **Lấy API key:**
> - **Cloudinary:** [cloudinary.com/console](https://cloudinary.com/console)
> - **Cloudflare R2:** Dashboard → R2 → Manage R2 API tokens
> - **Gemini:** [aistudio.google.com/app/apikey](https://aistudio.google.com/app/apikey)

### 3. Restore packages

```bash
dotnet restore
```

### 4. Chạy migration

```bash
dotnet ef database update \
  --project SocialApp.Infrastructure \
  --startup-project SocialApp.API
```

Migration `InitialCreate` sẽ tạo toàn bộ schema và seed tài khoản admin mặc định.

### 5. Chạy API

```bash
dotnet run --project SocialApp.API
```

API sẽ chạy tại:
- **HTTP:** `http://localhost:5000`
- **HTTPS:** `https://localhost:5001`
- **Swagger UI:** `http://localhost:5000/swagger`

---

## Tài khoản Admin mặc định

| Field | Giá trị |
|-------|---------|
| Email | `admin@socialapp.com` |
| Password | `Admin@123456` |
| Role | Admin |

> ⚠️ **Đổi mật khẩu ngay sau khi deploy production** qua endpoint `PUT /api/users/me/password`.

---

## Thứ tự test API trên Swagger

1. **Đăng ký tài khoản test**
   ```
   POST /api/auth/register
   ```

2. **Đăng nhập lấy token**
   ```
   POST /api/auth/login
   ```

3. **Paste token vào Swagger**
   - Click nút **Authorize** (🔒) góc trên phải
   - Nhập: `Bearer <accessToken>`

4. **Test theo thứ tự:**
   ```
   Users    → GET  /api/users/me
   Posts    → POST /api/posts
   Friends  → POST /api/friends/request/{userId}
   Messages → POST /api/messages/conversations
   AI Chat  → POST /api/ai/chat
   ```

5. **Test Admin** (dùng tài khoản admin):
   ```
   GET  /api/admin/dashboard
   GET  /api/admin/users
   GET  /api/admin/posts
   GET  /api/admin/cloud/stats
   ```

---

## Export API Contracts

Sau khi API đang chạy:

```bash
python tools/export_api_contracts.py \
  --swagger http://localhost:5000/swagger/v1/swagger.json \
  --output docs/api-contracts.md
```

---

## Cấu trúc Solution

```
SocialApp/
├── SocialApp.API/              ← Controllers, Hubs, Middleware, Program.cs
│   ├── Controllers/            ← AuthController, UsersController, PostsController...
│   ├── Hubs/                   ← ChatHub, NotificationHub
│   ├── Middleware/             ← GlobalExceptionMiddleware, BannedUserMiddleware
│   ├── Services/               ← ChatHubService, NotificationHubService
│   └── Extensions/             ← ServiceCollectionExtensions, ClaimsPrincipalExtensions
│
├── SocialApp.Application/      ← Business logic, Interfaces, DTOs
│   ├── Services/               ← AuthService, UserService, PostService...
│   ├── Interfaces/             ← IAuthService, IUserService...
│   ├── DTOs/                   ← Request/Response DTOs
│   ├── Validators/             ← FluentValidation validators
│   ├── Mappings/               ← AutoMapper profiles
│   └── Settings/               ← JwtSettings, GeminiSettings...
│
├── SocialApp.Domain/           ← Entities, Enums
│   ├── Entities/               ← User, Post, Message, Notification...
│   └── Enums/                  ← UserRole, PostPrivacy, MediaType...
│
├── SocialApp.Infrastructure/   ← EF Core, Repositories, Cloud services
│   ├── Data/                   ← AppDbContext, Migrations
│   ├── Repositories/           ← GenericRepository, UserRepository...
│   └── Services/               ← CloudinaryService, R2Service, CloudService
│
├── tools/
│   └── export_api_contracts.py ← Script xuất API docs
│
└── docs/
    ├── SETUP.md                ← File này
    └── api-contracts.md        ← Auto-generated từ Swagger
```

---

## SignalR Hubs

| Hub | Endpoint | Mô tả |
|-----|----------|-------|
| ChatHub | `/hubs/chat` | Real-time messaging |
| NotificationHub | `/hubs/notification` | Real-time notifications |

**Kết nối từ client (Angular/JS):**

```javascript
// Chat Hub
const chatConnection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/chat?access_token=YOUR_JWT_TOKEN')
  .withAutomaticReconnect()
  .build();

await chatConnection.start();

// Nhận tin nhắn
chatConnection.on('ReceiveMessage', (message) => {
  console.log('New message:', message);
});
```

---

## Troubleshooting

**`dotnet ef database update` thất bại:**
- Kiểm tra PostgreSQL đang chạy và connection string đúng
- Đảm bảo database user có quyền CREATE TABLE

**Build lỗi `IHttpClientFactory`:**
- Thêm vào `SocialApp.Application.csproj`:
  ```xml
  <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
  ```

**SignalR không kết nối được:**
- Kiểm tra `CorsSettings:AllowedOrigins` bao gồm origin của frontend
- Đảm bảo `AllowCredentials()` được bật trong CORS config

**Gemini API trả 403:**
- Kiểm tra `GeminiSettings:ApiKey` đúng
- API key phải có quyền truy cập Gemini API (không phải Vertex AI)