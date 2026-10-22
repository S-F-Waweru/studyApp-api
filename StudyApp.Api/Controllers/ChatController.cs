using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Chat;
using StudyApp.Domain.Enums;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ApiControllerBase
{
    private readonly IChatService _service;

    public ChatController(IChatService service) => _service = service;

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CreateChatSessionRequest request)
    {
        try { return Success(await _service.CreateSessionAsync(request)); }
        catch (InvalidOperationException ex) { return Fail(400, ex.Message); }
    }

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageRequest request)
    {
        try { return Success(await _service.SendMessageAsync(id, request)); }
        catch (ScopeUnavailableException ex) { return Fail(409, ex.Message); } // explicit, never silent widening
        catch (InvalidOperationException ex) { return Fail(404, ex.Message); }
    }

    [HttpGet("sessions/{id:guid}/scope")]
    public async Task<IActionResult> GetScope(Guid id) => Success(await _service.GetScopeSummaryAsync(id));

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<IActionResult> GetHistory(Guid id) =>
        Success(await _service.GetHistoryAsync(id));

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] Guid scopeId, [FromQuery] ScopeType scopeType) =>
        Success(await _service.GetSessionsByScopeAsync(scopeId, scopeType));

}
