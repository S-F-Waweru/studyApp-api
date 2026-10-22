using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyApp.Application.Embeddings;
using StudyApp.Application.Events;
using StudyApp.Application.Repositories;
using StudyApp.Application.Storage;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;
using StudyApp.Infrastructure.Embeddings;

namespace StudyApp.Infrastructure.Events;

// Runs in the background for the app's lifetime, one event at a time.
// This is the piece Note/Scribble/Document never talk to directly — they only publish DomainEvents.
public class EventProcessingWorker : BackgroundService
{
    private readonly Channel<DomainEvent> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventProcessingWorker> _logger;

    public EventProcessingWorker(Channel<DomainEvent> channel, IServiceScopeFactory scopeFactory, ILogger<EventProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var domainEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(domainEvent, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing event {EventType} for {SourceId}", domainEvent.EventType, domainEvent.SourceId);
            }
        }
    }

    private async Task ProcessAsync(DomainEvent domainEvent, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var vectorRepo = scope.ServiceProvider.GetRequiredService<IVectorChunkRepository>();
        var statusRepo = scope.ServiceProvider.GetRequiredService<ISourceIndexStatusRepository>();
        var extractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
        var chunker = scope.ServiceProvider.GetRequiredService<IDocumentChunker>();
        var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var sourceTypeStr = domainEvent.SourceType.ToString().ToLowerInvariant();

        // deletion: clean up existing chunks, nothing else to do
        if (domainEvent.EventType is DomainEventType.NoteDeleted or DomainEventType.ScribbleDeleted or DomainEventType.DocumentDeleted)
        {
            await vectorRepo.DeleteBySourceIdAsync(domainEvent.SourceId, ct);
            await statusRepo.MarkAsync(sourceTypeStr, domainEvent.SourceId, IndexStatus.Pending.ToString(), ct: ct);
            return;
        }

        // 1. fingerprint BEFORE any expensive work — this is what makes scribble autosave cheap
        var fingerprint = await extractor.FingerprintAsync(sourceTypeStr, domainEvent.SourceId, ct);
        var alreadyIndexed = await IsCurrentAsync(vectorRepo, statusRepo, sourceTypeStr, domainEvent.SourceId, fingerprint, ct);
        if (alreadyIndexed)
        {
            _logger.LogInformation("Unchanged {SourceType} {SourceId} — skipping re-embed", sourceTypeStr, domainEvent.SourceId);
            return;
        }

        await statusRepo.MarkAsync(sourceTypeStr, domainEvent.SourceId, IndexStatus.Extracting.ToString(), contentHash: fingerprint, ct: ct);

        // 2. extract (may call a vision model — hence the fingerprint check above)
        var extraction = await extractor.ExtractAsync(sourceTypeStr, domainEvent.SourceId, ct);
        if (extraction.FailureReason is not null)
        {
            await statusRepo.MarkAsync(sourceTypeStr, domainEvent.SourceId, IndexStatus.Failed.ToString(), error: extraction.FailureReason, ct: ct);
            _logger.LogWarning("Extraction failed for {SourceType} {SourceId}: {Reason}", sourceTypeStr, domainEvent.SourceId, extraction.FailureReason);
            return;
        }

        if (extraction.IsEmpty)
        {
            await statusRepo.MarkAsync(sourceTypeStr, domainEvent.SourceId, IndexStatus.Skipped.ToString(), ct: ct);
            _logger.LogInformation("No extractable content for {SourceType} {SourceId}", sourceTypeStr, domainEvent.SourceId);
            return;
        }

        // 3. chunk + dedupe, then embed everything BEFORE touching the existing index
        var specs = chunker.Chunk(extraction.Blocks);
        var seen = new HashSet<string>();
        var fresh = new List<VectorChunk>(specs.Count);

        foreach (var (spec, index) in specs.Select((s, i) => (s, i)))
        {
            if (!seen.Add(spec.ContentHash)) continue; // repeated headers/footers in PDFs

            var embedding = await embedder.EmbedAsync(spec.Text, ct);

            // Image bytes are persisted here, not in the chunker, so extraction/chunking stay IO-free.
            var imagePath = spec.ImageBytes is { Length: > 0 }
                ? (await storage.SaveAsync(
                        $"images/{sourceTypeStr}",
                        $"{domainEvent.SourceId:N}_{spec.ContentHash[..12]}{ImageExtension(spec.ImageContentType)}",
                        new MemoryStream(spec.ImageBytes)))
                    .Replace('\\', '/')   // storage-relative in DB, not OS-specific
                : null;

            fresh.Add(new VectorChunk
            {
                SourceId = domainEvent.SourceId,
                SourceType = sourceTypeStr,
                SourceTitle = spec.SourceTitle ?? extraction.SuggestedTitle,
                ScopeId = domainEvent.ScopeId,
                ScopeType = domainEvent.ScopeType,
                ChunkText = spec.EmbedText,
                HeadingPath = spec.HeadingPath,
                Locator = spec.Locator,
                Kind = spec.Kind,
                ChunkIndex = index,
                TokenCount = ExtractionPipeline.EstimateTokens(spec.EmbedText),
                ImagePath = imagePath,
                ContentHash = spec.ContentHash,
                PipelineVersion = ExtractionPipeline.Version,
                Embedding = new Pgvector.Vector(embedding)
            });
        }

        // 4. atomic swap
        await vectorRepo.ReplaceForSourceAsync(domainEvent.SourceId, fresh, ct);
        await statusRepo.MarkAsync(sourceTypeStr, domainEvent.SourceId, IndexStatus.Indexed.ToString(),
                                   chunkCount: fresh.Count, pageCount: extraction.PageCount, ct: ct);

        _logger.LogInformation("Indexed {Count} chunks for {SourceType} {SourceId}", fresh.Count, sourceTypeStr, domainEvent.SourceId);
    }

    private static async Task<bool> IsCurrentAsync(IVectorChunkRepository vectorRepo, ISourceIndexStatusRepository statusRepo,
        string sourceType, Guid sourceId, string fingerprint, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(fingerprint)) return false;

        var stored = await statusRepo.GetContentHashAsync(sourceType, sourceId, ct);
        if (stored != fingerprint) return false;

        var version = await statusRepo.GetIndexedPipelineVersionAsync(sourceId, ct);
        return version == ExtractionPipeline.Version;
    }

    private static string ImageExtension(string? contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}
