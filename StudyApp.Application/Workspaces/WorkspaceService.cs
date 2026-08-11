using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;


namespace StudyApp.Application.Workspaces;

public class WorkspaceService : IWorkSpaceService
{

    private readonly IWorkrepository _repository;
    public WorkspaceService(IWorkrepository repository) => _repository = repository;



    public async Task<WorkSpaceDto> CreateAsync(CreateWorspaceRequest request)
    {
        var workspace = new Workspace { Name = request.Name };
        await _repository.CreateAsync(workspace);
        return ToDto(workspace);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var workspace = await this.GetByIsAsync(id);
        if (workspace is null) return false;

        await _repository.DeleteAsync(workspace.Id);

        return true;

    }

    public async Task<IEnumerable<WorkSpaceDto>> GetAllAsync()
    {
        var workspaces = await _repository.GetAllAsync();
        return workspaces.Select(ToDto);
    }

    public async Task<WorkSpaceDto?> GetByIsAsync(Guid id)
    {
        var workspace = await _repository.GetByIdAsync(id);
        return workspace is null ? null : ToDto(workspace);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateWorkspaceRequest request)
    {
        var workspace = await _repository.GetByIdAsync(id);
        if (workspace is null) return false;

        workspace.Name = request.Name;

        await _repository.UpdateAsync(workspace);
        return true;

    }


    private static WorkSpaceDto ToDto(Workspace w) => new(w.Id, w.Name, w.CreatedAt);


}
