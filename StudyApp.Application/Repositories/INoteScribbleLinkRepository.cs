namespace StudyApp.Application.Repositories;

public interface INoteScribbleLinkRepository
{
    Task LinkAsync(Guid noteId, Guid scribbleId);
    Task UnlinkAsync(Guid noteId, Guid scribbleId);
    Task<IEnumerable<Guid>> GetLinkedScribbleIdsAsync(Guid noteId);
    Task<IEnumerable<Guid>> GetLinkedNoteIdsAsync(Guid scribbleId);
}
