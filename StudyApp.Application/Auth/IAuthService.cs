namespace StudyApp.Application.Auth;

public interface IAuthService
{
    Task <AuthTokenDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    // null = bad credentials or locked out. The caller maps that to 401 without
    // knowing which of the two it was, so the response cannot be used to probe accounts.
    Task<AuthTokenDto?> LoginAsync(LoginRequest request, CancellationToken ct = default);

    // null = unknown, revoked, replayed or expired token
    Task<AuthTokenDto?> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    
    // idempotent: logging out an already-dead token is not an error
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);

    Task LogoutAllAsync(Guid userId, CancellationToken ct = default);
}