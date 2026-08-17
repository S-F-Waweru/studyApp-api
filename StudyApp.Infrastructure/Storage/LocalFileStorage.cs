using StudyApp.Application.Storage;

namespace StudyApp.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(string relativeFolder, string filename, Stream content)
    {
        var folder = Path.Combine(_rootPath, relativeFolder);
        Directory.CreateDirectory(folder);

        var relativePath = Path.Combine(relativeFolder, filename);
        var fullPath = Path.Combine(_rootPath, relativePath);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream);

        return relativePath;
    }

    public Task<Stream> OpenReadAsync(string relativePath) =>
        Task.FromResult<Stream>(File.OpenRead(Path.Combine(_rootPath, relativePath)));

    public Task DeleteAsync(string relativePath)
    {
        var fullPath = Path.Combine(_rootPath, relativePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
