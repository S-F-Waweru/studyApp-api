using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Notes;
using StudyApp.Domain.Enums;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/notes")]
public class NotesController : ApiControllerBase
{
    private readonly INoteService _service;

    public NotesController(INoteService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(CreateNoteRequest request)
    {
        var result = await _service.CreateAsync(request);
        Console.WriteLine($"----------------------------------------->Controller<----------------------------------------");
        Console.WriteLine(result);
        Console.WriteLine($"-----------------------------------------><----------------------------------------");

        // return CreatedAtRoute("GetNoteById", new { id = result.Id }, result);

            Response.Headers.Location = Url.RouteUrl("GetNoteById", new { id = result.Id });
            return Success(result, 201);
    }

    [HttpGet("{id:guid}", Name = "GetNoteById")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        Console.WriteLine($"----------------------------------------->Controller : GetNoteById<----------------------------------------");
        Console.WriteLine(result);
        Console.WriteLine($"-----------------------------------------><----------------------------------------");

        return result is null ? Fail(404, "Note not found") : Success(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByScope([FromQuery] Guid scopeId, [FromQuery] ScopeType scopeType) =>
            Success(await _service.GetByScopeAsync(scopeId, scopeType));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNoteRequest request)
    {
        var success = await _service.UpdateAsync(id, request);
        return success ? Success<object?>(null, 204) : Fail(404, "Note not found");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _service.DeleteAsync(id);
        return success ? Success<object?>(null, 204) : Fail(404, "Note not found");
    }
}
