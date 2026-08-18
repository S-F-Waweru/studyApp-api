using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Chat;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _service;

    public ChatController(IChatService service) => _service = service;

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CreateChatSessionRequest request) =>
        Ok(await _service.CreateSessionAsync(request));

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<IActionResult> GetHistory(Guid id) =>
        Ok(await _service.GetHistoryAsync(id));

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageRequest request) =>
        Ok(await _service.SendMessageAsync(id, request));
}
