using Microsoft.AspNetCore.Identity;
using StudyApp.Application.Auth;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;

namespace StudyApp.Infrastructure.Identity;

public class AuthService : IAuthService
{
    
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    // dead tokens are kept for a month so a late replay is still recognised as
    // reuse instead of looking like an unknown token, then they are deleted
    private static readonly TimeSpan RefreshTokenRetention = TimeSpan.FromDays(30);
    
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;

    public AuthService(UserManager<AppUser> users, ITokenService tokens, IRefreshTokenRepository refreshTokens)
    {
        _users = users;
        _tokens = tokens;
        _refreshTokens = refreshTokens;

    }
    
    public async Task<AuthTokenDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();

        if (await _users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("An account with that email already exists.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? email.Split('@')[0]
                : request.DisplayName.Trim()
        };

        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                string.Join(" ", created.Errors.Select(e => e.Description)));

        return  await IssueAsync(user, Guid.NewGuid(), ct);
    }


    public async Task<AuthTokenDto?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return null;
        
        // the Lockout.* values in Program.cs are inert on their own: CheckPasswordAsync
        // only compares hashes and never touches the counters, so both the check and
        // the bookkeeping have to happen here
        if (await _users.IsLockedOutAsync(user)) return null;
        
        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user); // the fifth failure sets LockoutEnd
            return null;
        }
        
        // a login is a new device: own family, so revoking it later cannot sign
        // the user out of their other devices
        return await IssueAsync(user, Guid.NewGuid(), ct);
    }

    public async Task<AuthTokenDto?> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return null;
        
        var stored = await _refreshTokens.GetByHashAsync(_tokens.HashRefreshToken(request.RefreshToken), ct);
        if (stored is null) return null;
        
        // a token that was already rotated is being replayed — either a thief or a
        // client that lost the response. Either way the entire chain is suspect.
        if (stored.RevokedAtUtc is not null)
        {
            await _refreshTokens.RevokeFamilyAsync(stored.FamilyId, "reuse-detected", ct);
            return null;
        }

        if (stored.ExpiresAtUtc <= DateTime.UtcNow) return null;
        
        var user = await _users.FindByIdAsync(stored.UserId.ToString());
        if (user is null) return null;
        
        var (accessToken, accessExpires) = _tokens.CreateAccessToken(user.Id, user.Email!);
        var (replacement, rawRefresh, refreshExpires) = NewRefreshToken(user.Id, stored.FamilyId);

        
        await _refreshTokens.RotateAsync(stored, replacement, ct);

        return new AuthTokenDto(accessToken, accessExpires, rawRefresh, refreshExpires, ToDto(user));
        
    }


    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        return user is null ? null : ToDto(user);
    }



    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null) return false;

        var result = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if(!result.Succeeded) return false;
        // a password change is the user saying "assume every existing session is suspect"
        await _refreshTokens.RevokeAllForUserAsync(userId, "password-changed", ct);
        
        return  true;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return;
        
        var stored = await _refreshTokens.GetByHashAsync(_tokens.HashRefreshToken(request.RefreshToken), ct);
        if (stored is null) return;
        
        // the family, not the single row: leaving siblings alive would let a slightly
        // stale copy of this device's token keep working after logout
        await _refreshTokens.RevokeFamilyAsync(stored.FamilyId, "logout", ct);
    }

    public Task LogoutAllAsync(Guid userId, CancellationToken ct = default) =>
        _refreshTokens.RevokeAllForUserAsync(userId, "logout-all", ct);

    
    
    private async Task<AuthTokenDto> IssueAsync(AppUser user, Guid familyId, CancellationToken ct)
    {
        var (accessToken, accessExpires) = _tokens.CreateAccessToken(user.Id, user.Email!);
        var (record, rawRefresh, refreshExpires) = NewRefreshToken(user.Id, familyId);

        await _refreshTokens.AddAsync(record, ct);
        await _refreshTokens.PruneExpiredAsync(user.Id, DateTime.UtcNow - RefreshTokenRetention, ct);

        return new AuthTokenDto(accessToken, accessExpires, rawRefresh, refreshExpires, ToDto(user));
    }

    private (RefreshToken Record, string Raw, DateTime ExpiresAtUtc) NewRefreshToken(Guid userId, Guid familyId)
    {
        var raw = _tokens.CreateRefreshToken();
        var expires = DateTime.UtcNow.Add(RefreshTokenLifetime);

        var record = new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = _tokens.HashRefreshToken(raw),
            ExpiresAtUtc = expires
        };

        return (record, raw, expires);
    }

    private static UserDto ToDto(AppUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName);
}