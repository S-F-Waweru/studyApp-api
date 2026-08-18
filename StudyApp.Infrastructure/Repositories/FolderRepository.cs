using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class FolderRepository : IFolderRepository
{
    private readonly AppDbContext _context;

    public FolderRepository(AppDbContext context){
        _context = context;
    }




    public async Task AddAsync(Folder folder)
    {
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var folder =await GetByIdAsync(id);
        if (folder is not null) {
            _context.Folders.Remove(folder);
            await _context.SaveChangesAsync();
        };

    }

    public async Task<Folder?> GetByIdAsync(Guid id)
    =>
       await _context.Folders.FindAsync(id);

    public async Task UpdateAsync(Folder folder)
       {
           _context.Folders.Update(folder);
           await _context.SaveChangesAsync();
       }


    public async Task<IEnumerable<Folder>> GetChildrenAsync(Guid? parentFolderId, Guid workspaceId)
    =>
        await _context.Folders
            .Where(
            f => f.WorkspaceId == workspaceId &&
            f.ParentFolderId == parentFolderId
            )
            .OrderBy(f => f.Name)
            .ToListAsync();


    public async Task<IEnumerable<Guid>> GetDescendantIdsAsync(Guid folderId)
    {
        const string sql = @"
            WITH RECURSIVE descendants AS (
            SELECT ""Id"" FROM ""Folders"" WHERE ""Id"" =  @folderId
            UNION ALL
            SELECT f.""Id"" FROM ""Folders"" f
            INNER JOIN descendants d ON f.""ParentFolderId"" = d.""Id""
            )
            SELECT ""Id"" FROM descendants;
        ";

        var connections = _context.Database.GetDbConnection();
        await connections.OpenAsync();

        await using var command = connections.CreateCommand();
        command.CommandText = sql;

        var param = command.CreateParameter();
        param.ParameterName = "@folderId";
        param.Value = folderId;
        command.Parameters.Add(param);


        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));

        return ids;

    }


    public async Task<IEnumerable<Guid>> GetAllFolderIdsInWorkspaceAsync(Guid workspaceId)
    {
        const string sql = @"
            WITH RECURSIVE descendants AS (
                SELECT ""Id"" FROM ""Folders"" WHERE ""WorkspaceId"" = @workspaceId AND ""ParentFolderId"" IS NULL
                UNION ALL
                SELECT f.""Id"" FROM ""Folders"" f
                INNER JOIN descendants d ON f.""ParentFolderId"" = d.""Id""
            )
            SELECT ""Id"" FROM descendants;";

        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var param = command.CreateParameter();
        param.ParameterName = "@workspaceId";
        param.Value = workspaceId;
        command.Parameters.Add(param);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));

        return ids;
    }

}
