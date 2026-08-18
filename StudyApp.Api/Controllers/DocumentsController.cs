using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Documents;
using StudyApp.Domain.Enums;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ApiControllerBase
{
    private readonly IDocumentService _service;

    public DocumentsController(IDocumentService service) => _service = service;

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] Guid scopeId, [FromForm] ScopeType scopeType, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var result = await _service.UploadAsync(scopeId, scopeType, file.FileName, stream);
        return Success(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByScope([FromQuery] Guid scopeId, [FromQuery] ScopeType scopeType) =>
        Success(await _service.GetByScopeAsync(scopeId, scopeType));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _service.DeleteAsync(id);
        // return success ? NoContent() : NotFound();
        return success ? Success<object?>(null, 204) : Fail(404, "Document not found");

    }
}
