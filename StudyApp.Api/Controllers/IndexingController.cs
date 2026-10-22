using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Indexing;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/indexing")]
public class IndexingController : ApiControllerBase
{
    private readonly IIndexReindexService _reindex;

    public IndexingController(IIndexReindexService reindex) => _reindex = reindex;

    [HttpPost("reindex/{sourceType}/{sourceId:guid}")]
    public async Task<IActionResult> ReindexSource(string sourceType, Guid sourceId, CancellationToken ct) =>
        Success(new { queued = await _reindex.ReindexSourceAsync(sourceType, sourceId, ct) });

    [HttpPost("reindex")]
    public async Task<IActionResult> ReindexAll(CancellationToken ct) =>
        Success(new { queued = await _reindex.ReindexStaleAsync(500, ct) });
}