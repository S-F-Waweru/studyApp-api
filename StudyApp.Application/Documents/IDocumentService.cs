using StudyApp.Domain.Enums;

namespace StudyApp.Application.Documents;

public interface IDocumentService
{
    Task<DocumentDto> UploadAsync(Guid scopeId, ScopeType scopeType, string filename, Stream content);
    Task<IEnumerable<DocumentDto>> GetByScopeAsync(Guid scopeId, ScopeType scopeType);
    Task<bool> DeleteAsync(Guid id);
}
