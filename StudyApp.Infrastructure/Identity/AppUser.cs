// StudyApp.Infrastructure/Identity/AppUser.cs
using Microsoft.AspNetCore.Identity;

namespace StudyApp.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}