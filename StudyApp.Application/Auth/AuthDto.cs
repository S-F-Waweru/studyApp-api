using System.ComponentModel.DataAnnotations;

namespace StudyApp.Application.Auth;


    public record RegisterRequest(string Email, string Password, string? DisplayName);
    public record LoginRequest(String Email, String Password);
    
    public record ChangePasswordRequest(String CurrentPassword, String NewPassword);
    
    public record UserDto(Guid Id, string Email, string? DisplayName);
    
    public record RefreshRequest(String RefreshToken);

    public record LogoutRequest(String RefreshToken);

    public record AuthTokenDto(
        String AccessToken,
        DateTime ExpiresAtUtc,
        String RefreshToken,
        DateTime RefreshTokenExpiresAtUtc,
        UserDto User);