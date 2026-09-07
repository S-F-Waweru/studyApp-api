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
    public async Task<IActionResult> CreateSession(CreateChatSessionRequest request) =>
        Success(await _service.CreateSessionAsync(request));

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<IActionResult> GetHistory(Guid id) =>
        Success(await _service.GetHistoryAsync(id));

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageRequest request) =>
        Success(await _service.SendMessageAsync(id, request));


    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] Guid scopeId, [FromQuery] ScopeType scopeType) =>
        Success(await _service.GetSessionsByScopeAsync(scopeId, scopeType));

}
