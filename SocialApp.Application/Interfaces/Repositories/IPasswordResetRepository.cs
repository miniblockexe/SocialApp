using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

public interface IPasswordResetRepository
{
    Task<PasswordResetToken?> GetActiveTokenAsync(string email, string otp);
    Task<PasswordResetToken?> GetByVerifyTokenAsync(string email, string verifyToken);
    Task DeleteAllForUserAsync(Guid userId);
    Task AddAsync(PasswordResetToken token);
    Task SaveChangesAsync();
}