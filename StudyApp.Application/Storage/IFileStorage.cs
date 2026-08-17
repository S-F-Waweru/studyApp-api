namespace StudyApp.Application.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(string relativeFolder, string filename, Stream content);
    Task<Stream> OpenReadAsync(string relativePath);
    Task DeleteAsync(string relativePath);
}
