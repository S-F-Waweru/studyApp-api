using StudyApp.Domain.Entities;

namespace StudyApp.Application.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    // revokes the presented token and inserts its replacement in one SaveChanges
    Task RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken ct = default);

    Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default);

    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);

    // bounded cleanup: expired tokens serve no purpose once they can no longer be refreshed
    Task<int> PruneExpiredAsync(Guid userId, DateTime expiredBeforeUtc, CancellationToken ct = default);
}