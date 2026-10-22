namespace StudyApp.Application.Retrieval;

public class RetrievalOptions
{
    public double MinScore { get; set; } = 0.55;       // tune per embedding model; log the scores you see
    public int VectorCandidateCount { get; set; } = 24; // over-fetch, then filter
    public int KeywordCandidateCount { get; set; } = 16;
    public int FinalCount { get; set; } = 8;
    public int MaxPerSource { get; set; } = 3;          // diversity: one PDF must not dominate
    public int MaxSources { get; set; } = 6;
    public int MaxContextTokens { get; set; } = 6_000;  // total prompt budget for retrieved context
    public bool HybridEnabled { get; set; } = true;
    public int RrfK { get; set; } = 60;                 // reciprocal rank fusion constant
}