namespace StudyApp.Application.Events;

public interface IEventPublisher
{
    Task PublishAsync(DomainEvent domainEvent);
}
