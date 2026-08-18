using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Folders;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/folders")]
public class FoldersController : ControllerBase
{
    private readonly IFolderService _service;

    public FoldersController(IFolderService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFolderRequest request)
    {
        var result = await _service.CreateAsync(request);

        return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }

        [HttpGet("{id:guid}", Name = "GetFolderById")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);

            return result is null ? NotFound() : Ok(result);
        }

    [HttpGet]
    public async Task<IActionResult> GetChildren([FromQuery] Guid workspaceId, [FromQuery] Guid? parentFolderId) =>
            Ok(await _service.GetChildrenAsync(parentFolderId, workspaceId));

    [HttpGet("{id:guid}/descendants")]
    public async Task<IActionResult> GetDescendants(Guid id) =>
                    Ok(await _service.GetDescendantScopeIdsAsync(id));
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateFolderRequest request)
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
