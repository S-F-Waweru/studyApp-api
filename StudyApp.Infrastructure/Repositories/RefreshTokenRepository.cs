using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class RefreshTokenRepository :IRefreshTokenRepository
{
    
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }
    
    
    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _context.RefreshTokens.Add(token);
        return _context.SaveChangesAsync(ct);
    }

    // tracked on purpose: RotateAsync mutates this instance and relies on
    // change tracking instead of re-attaching it explicitly
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    

    public Task RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken ct = default)
    {
        current.RevokedAtUtc = DateTime.UtcNow;
        current.ReplacedByTokenId = replacement.Id;
        current.RevocationReason = "Rotated";
        
        _context.RefreshTokens.Add(replacement);

        // one SaveChanges = one transaction: the revoke and the insert either
        // both land or neither does, so a crash can never consume a token
        return _context.SaveChangesAsync(ct);
    }

    public async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow; 
        
        // set-based update: a replay attempt must revoke every descendant at once,
        // and it must not be defeated by stale tracked entities in this context
        await _context.RefreshTokens.
                Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAtUtc, now)
                .SetProperty(t => t.RevocationReason, reason)
                , ct
            );

    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAtUtc, now)
                .SetProperty(t => t.RevocationReason, reason)
                , ct
            );
    }

    public Task<int> PruneExpiredAsync(Guid userId, DateTime expiredBeforeUtc, CancellationToken ct = default) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAtUtc < expiredBeforeUtc)
            .ExecuteDeleteAsync(ct);
}