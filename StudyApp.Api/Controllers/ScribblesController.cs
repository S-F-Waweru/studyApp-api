using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Scribbles;
using StudyApp.Domain.Enums;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/scribbles")]
public class ScribblesController : ApiControllerBase
{
    private readonly IScribbleService _service;

    public ScribblesController(IScribbleService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(CreateScribbleRequest request)
    {
        var result = await _service.CreateAsync(request);
        // return CreatedAtRoute(nameof(GetById), new { id = result.Id }, result);
        Response.Headers.Location = Url.RouteUrl("GetScribbleById", new { id = result.Id });
        return Success(result, 201);
    }

    [HttpGet("{id:guid}", Name = "GetScribbleById")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        // return result is null ? NotFound() : Ok(result);
         return result is null ? Fail(404, "Scribble not found") : Success(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByScope([FromQuery] Guid scopeId, [FromQuery] ScopeType scopeType) =>
            Success(await _service.GetByScopeAsync(scopeId, scopeType));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateScribbleRequest request)
    {
        var success = await _service.UpdateAsync(id, request);
        // return success ? NoContent() : NotFound();
        return success ? Success<object?>(null, 204) : Fail(404, "Scribble not found");

    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        Console.WriteLine($"==> Delete {id}");
        var success = await _service.DeleteAsync(id);
        // return success ? NoContent() : NotFound();
        return success ? Success<object?>(null, 204) : Fail(404, "Scribble not found");
    }

    [HttpPost("{id:guid}/link/{noteId:guid}")]
    public async Task<IActionResult> Link(Guid id, Guid noteId)
    {
        await _service.LinkToNoteAsync(id, noteId);
        // return NoContent();
        return Success<object?>(null, 204);
    }

    [HttpDelete("{id:guid}/link/{noteId:guid}")]
    public async Task<IActionResult> Unlink(Guid id, Guid noteId)
    {
        await _service.UnlinkFromNoteAsync(id, noteId);
        // return NoContent();
        return Success<object?>(null, 204);
    }
}
