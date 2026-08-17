using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Scribbles;
using StudyApp.Domain.Enums;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/scribbles")]
public class ScribblesController : ControllerBase
{
    private readonly IScribbleService _service;

    public ScribblesController(IScribbleService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(CreateScribbleRequest request)
    {
        var result = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByScope([FromQuery] Guid scopeId, [FromQuery] ScopeType scopeType) =>
        Ok(await _service.GetByScopeAsync(scopeId, scopeType));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateScribbleRequest request)
    {
        var success = await _service.UpdateAsync(id, request);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _service.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/link/{noteId:guid}")]
    public async Task<IActionResult> Link(Guid id, Guid noteId)
    {
        await _service.LinkToNoteAsync(id, noteId);
        return NoContent();
    }

    [HttpDelete("{id:guid}/link/{noteId:guid}")]
    public async Task<IActionResult> Unlink(Guid id, Guid noteId)
    {
        await _service.UnlinkFromNoteAsync(id, noteId);
        return NoContent();
    }
}
