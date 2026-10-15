using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Ai;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiSettingsController : ApiControllerBase
{
    private readonly IAiSettingsService _settings;
    private readonly IAiConnectionTester _tester;

    public AiSettingsController(IAiSettingsService settings, IAiConnectionTester tester)
    {
        _settings = settings;
        _tester = tester;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Get() => Success(await _settings.GetAsync());

    [HttpPut("settings")]
    public async Task<IActionResult> Update(UpdateAiSettingsRequest request)
    {
        try
        {
            return Success(await _settings.UpdateAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return Fail(400, ex.Message);
        }
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(TestAiRequest request) => Success(await _tester.TestAsync(request));
}
