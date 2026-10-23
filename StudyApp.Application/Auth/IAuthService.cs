namespace StudyApp.Application.Auth;

public interface IAuthService
{
    Task <AuthTokenDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthTokenDto> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}