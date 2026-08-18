

using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class WorkspaceRepository : IWorkrepository
{
    private readonly AppDbContext _context;

    public WorkspaceRepository(AppDbContext context){
        _context = context;
    }


    public  async Task CreateAsync(Workspace workspace)
    {
         _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var workspace = await _context.Workspaces.FindAsync(id);
        if (workspace is not null)
        {
            _context.Workspaces.Remove(workspace);
            await _context.SaveChangesAsync();
        }
    }
    public async Task<IEnumerable<Workspace>> GetAllAsync() => await _context.Workspaces.OrderByDescending(w => w.CreatedAt).ToListAsync();

    public async Task<Workspace?> GetByIdAsync(Guid id) =>
            await _context.Workspaces.FindAsync(id);

    public async Task UpdateAsync(Workspace workspace)
    {
        _context.Workspaces.Update(workspace);
        await _context.SaveChangesAsync();
    }

}
