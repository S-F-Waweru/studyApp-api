using StudyApp.Domain.Entities;

namespace StudyApp.Application.Repositories;

public interface IAiSettingsRepository
{
    Task<AiSettings> GetAsync(); // falls back to appsettings defaults until the first save
    Task UpdateAsync(AiSettings settings);
}