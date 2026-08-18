using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using StudyApp.Application.Workspaces;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController : ApiControllerBase
{
    private readonly IWorkSpaceService _service;

    public WorkspacesController(IWorkSpaceService service)
    {
        _service = service;
    }


    [HttpPost]
    public async Task<IActionResult> Create(CreateWorspaceRequest request) {
        var result = await _service.CreateAsync(request);
            // return CreatedAtRoute("GetWorkspaceById", new { id = result.Id }, result);
            //
            Response.Headers.Location = Url.RouteUrl("GetWorkspaceById", new { id = result.Id });
            return Success(result, 201);


    }

    [HttpGet("{id:Guid}", Name = "GetWorkspaceById")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIsAsync(id);
        // return result is null ? NotFound() : Ok(result);
        return result is null ? Fail(404, "Note not found") : Success(result);


    }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Success(await _service.GetAllAsync());

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, UpdateWorkspaceRequest request)
        {
            var success = await _service.UpdateAsync(id, request);
            return success ? Success<object?>(null, 204) : Fail(404, "WorksSpace not found");

        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _service.DeleteAsync(id);
            return success ? Success<object?>(null, 204) : Fail(404, "WorkSpace not found");

        }


}
