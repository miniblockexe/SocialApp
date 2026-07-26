using Microsoft.AspNetCore.Http;

namespace SocialApp.Application.DTOs.Users;

public sealed class FileUploadRequest
{
    public IFormFile? File { get; init; }
}