namespace StudyApp.Application.Ai;

public interface IAiSettingsService
{
    Task<AiSettingsDto> GetAsync();
    Task<AiSettingsDto> UpdateAsync(UpdateAiSettingsRequest request);
}