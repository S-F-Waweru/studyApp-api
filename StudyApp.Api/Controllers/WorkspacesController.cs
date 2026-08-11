using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using StudyApp.Application.Workspaces;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkSpaceService _service;

    public WorkspacesController(IWorkSpaceService service)
    {
        _service = service;
    }


    [HttpPost]
    public async Task<IActionResult> Create(CreateWorspaceRequest request) {
        var result = await _service.CreateAsync(request);

        return CreatedAtAction(nameof(GetById), new { id = request }, result);


    }

    [HttpGet("{id:Guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIsAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, UpdateWorkspaceRequest request)
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


}
