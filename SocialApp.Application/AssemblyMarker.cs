namespace SocialApp.Application;

/// <summary>
/// Marker class — chỉ dùng để trỏ assembly khi đăng ký AutoMapper và FluentValidation.
/// services.AddAutoMapper(typeof(AssemblyMarker).Assembly)
/// services.AddValidatorsFromAssembly(typeof(AssemblyMarker).Assembly)
/// </summary>
public sealed class AssemblyMarker { }