using Microsoft.Extensions.Logging;
using StudyApp.Application.Events;

namespace StudyApp.Infrastructure.Events;

public class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) => _logger = logger;

    public Task PublishAsync(DomainEvent domainEvent)
    {
        _logger.LogInformation(
            "Event: {EventType} sourceType={SourceType} source={SourceId} scope={ScopeId}/{ScopeType}",
            domainEvent.EventType, domainEvent.SourceType, domainEvent.SourceId, domainEvent.ScopeId, domainEvent.ScopeType);
        return Task.CompletedTask;
    }
}
