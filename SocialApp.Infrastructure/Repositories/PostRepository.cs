using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Repository cho Post — extend GenericRepository, thêm AddMediaFilesAsync.
/// </summary>
public sealed class PostRepository : GenericRepository<Post>, IPostRepository
{
    private readonly AppDbContext _ctx;

    public PostRepository(AppDbContext context) : base(context)
    {
        _ctx = context;
    }

    /// <inheritdoc/>
    public async Task AddMediaFilesAsync(IEnumerable<PostMediaFile> files, CancellationToken ct = default)
    {
        await _ctx.PostMediaFiles.AddRangeAsync(files, ct);
        await _ctx.SaveChangesAsync(ct);
    }
}