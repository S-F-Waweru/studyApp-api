namespace StudyApp.Domain.Enums;

public enum IndexStatus
{
    Pending = 0,
    Extracting = 1,
    Indexed= 2,
    Unchanged = 3, // fingerprint matched, embedding skipped
    Skipped = 4,   // nothing extractable (e.g. an empty canvas)
    Failed=5
}