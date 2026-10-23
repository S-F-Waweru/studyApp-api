using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StudyApp.Application.Auth;

namespace StudyApp.Infrastructure.Identity;

public class TokenService: ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public TokenService(JwtOptions options)
    {
       _options = options;
       if(Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
           throw new ArgumentException("Jwt:SigningKey must be at least 32 bytes. Set Jwt__SigningKey as an environment variable.");

       _credentials = new SigningCredentials(
           new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
           SecurityAlgorithms.HmacSha256
       );
    }
    
    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(Guid userId, string email)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);


        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            notBefore: DateTime.UtcNow,
            expires: expires,
            claims: claims,
            signingCredentials: _credentials
        );
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
    
    
    public string CreateRefreshToken()
    {
        // 256 bits of entropy, URL-safe: this string travels through JSON bodies,
        // and one day through headers or query strings, without re-encoding
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}