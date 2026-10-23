namespace StudyApp.Infrastructure.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    
    public string Issuer { get; set; } = "studyapp-api";
    public string Audience { get; set; } = "studyapp-angular";
    public string SigningKey {get; set;} = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;

}