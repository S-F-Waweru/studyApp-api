using StudyApp.Domain.Entities;

namespace StudyApp.Application.Repositories;

public interface IWorkrepository
{
    Task<Workspace?> GetByIdAsync(Guid id);
    Task<IEnumerable<Workspace>> GetAllAsync();
    Task CreateAsync(Workspace workspace);
    Task UpdateAsync(Workspace workspace);
    Task DeleteAsync(Guid id);
}
