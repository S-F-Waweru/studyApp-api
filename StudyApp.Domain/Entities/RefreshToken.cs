namespace StudyApp.Domain.Entities;

// One row per issued refresh token. Rotation never deletes a row: a token that
// arrives already revoked is the evidence that someone replayed a stolen copy.
public class RefreshToken
{
  public Guid Id {get; set;} =  Guid.NewGuid();
  public  Guid UserId { get; set; }
  
  // every token descended from one login shares this id, so one revocation
  // can kill a whole device's chain without touching other devices
  public Guid FamilyId { get; set; }
  
  // only the SHA-256 of the token is stored — the raw value exists once,
  // in the response that created it, and is never recoverable from the database
  public string TokenHash { get; set; } = default!;
  
  public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
  public DateTime ExpiresAtUtc { get; set; }
  public DateTime? RevokedAtUtc { get; set; }
  
  // points at the token that replaced this one during rotation
  public Guid? ReplacedByTokenId { get; set; }
  
  // "rotated" | "logout" | "logout-all" | "reuse-detected"
  public string? RevocationReason { get; set; }
}