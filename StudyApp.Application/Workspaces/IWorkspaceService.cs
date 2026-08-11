namespace StudyApp.Application.Workspaces;

public interface IWorkSpaceService
{
    Task<WorkSpaceDto> CreateAsync(CreateWorspaceRequest request);
    Task<WorkSpaceDto?> GetByIsAsync(Guid id);
    Task<IEnumerable<WorkSpaceDto>> GetAllAsync();
    Task<bool> UpdateAsync(Guid id, UpdateWorkspaceRequest request);
    Task<bool> DeleteAsync(Guid id);
}
