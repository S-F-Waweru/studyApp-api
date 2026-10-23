namespace StudyApp.Application.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(Guid userId, string email);
}