using Microsoft.AspNetCore.Identity;
using StudyApp.Application.Auth;

namespace StudyApp.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;

    public AuthService(UserManager<AppUser> users, ITokenService tokens)
    {
        _users = users;
        _tokens = tokens;
        
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

        return Issue(user);
    }


    public async Task<AuthTokenDto?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return null;

        return await _users.CheckPasswordAsync(user, request.Password)
            ? Issue(user)
            : null;
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
        return result.Succeeded;
    }
    
    
    private static UserDto ToDto(AppUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName);
    
    private AuthTokenDto Issue(AppUser user)
    {
        var (token, expiresAtUtc) = _tokens.CreateAccessToken(user.Id, user.Email!);
        return new AuthTokenDto(token, expiresAtUtc, ToDto(user));
    }
}