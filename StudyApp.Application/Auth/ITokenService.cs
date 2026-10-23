namespace StudyApp.Application.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(Guid userId, string email);
    
    // opaque and high-entropy — deliberately not a JWT, so there are no claims in it
    // to tamper with and no signature for an attacker to attack
    string CreateRefreshToken();
    
    // the only form the database ever sees
    string HashRefreshToken(string refreshToken);
}