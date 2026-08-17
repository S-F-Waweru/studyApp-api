using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyApp.Application.Embeddings;
using StudyApp.Application.Events;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
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
                await ProcessAsync(domainEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing event {EventType} for {SourceId}", domainEvent.EventType, domainEvent.SourceId);
            }
        }
    }

    private async Task ProcessAsync(DomainEvent domainEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var vectorRepo = scope.ServiceProvider.GetRequiredService<IVectorChunkRepository>();

        // deletion: clean up existing chunks, nothing else to do
        if (domainEvent.EventType is DomainEventType.NoteDeleted or DomainEventType.ScribbleDeleted or DomainEventType.DocumentDeleted)
        {
            await vectorRepo.DeleteBySourceIdAsync(domainEvent.SourceId);
            return;
        }

        // create/update: re-extract, re-chunk, re-embed, replace existing chunks for this source
        var extractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
        var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

        var sourceTypeStr = domainEvent.SourceType.ToString().ToLowerInvariant();
        var text = await extractor.ExtractAsync(sourceTypeStr, domainEvent.SourceId);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("No extractable text for {SourceType} {SourceId} — skipping embedding", sourceTypeStr, domainEvent.SourceId);
            return;
        }

        await vectorRepo.DeleteBySourceIdAsync(domainEvent.SourceId); // clear stale chunks before writing fresh ones

        var chunks = TextChunker.Chunk(text);
        var vectorChunks = new List<VectorChunk>();
        foreach (var chunkText in chunks)
        {
            var embedding = await embedder.EmbedAsync(chunkText);
            vectorChunks.Add(new VectorChunk
            {
                SourceId = domainEvent.SourceId,
                SourceType = sourceTypeStr,
                ScopeId = domainEvent.ScopeId,
                ScopeType = domainEvent.ScopeType,
                ChunkText = chunkText,
                Embedding = new Pgvector.Vector(embedding)
            });
        }

        await vectorRepo.AddRangeAsync(vectorChunks);
        _logger.LogInformation("Embedded {Count} chunks for {SourceType} {SourceId}", vectorChunks.Count, sourceTypeStr, domainEvent.SourceId);
    }
}
