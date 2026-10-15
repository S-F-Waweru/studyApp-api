namespace StudyApp.Application.Ai;

public interface IAiConnectionTester
{
    Task<AiTestReport> TestAsync(TestAiRequest request);
}