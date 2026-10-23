using System.ComponentModel.DataAnnotations;

namespace StudyApp.Application.Auth;


    public record RegisterRequest(string Email, string Password, string? DisplayName);
    public record LoginRequest(String Email, String Password);
    
    public record ChangePasswordRequest(String CurrentPassword, String NewPassword);
    
    public record UserDto(Guid Id, string Email, string? DisplayName);
    
    public record AuthTokenDto(String AccessToken, DateTime ExpiresAtUtc, UserDto User);