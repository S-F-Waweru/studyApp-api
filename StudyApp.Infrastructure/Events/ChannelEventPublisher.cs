using System.Threading.Channels;
using StudyApp.Application.Events;

namespace StudyApp.Infrastructure.Events;

public class ChannelEventPublisher : IEventPublisher
{
    private readonly Channel<DomainEvent> _channel;

    public ChannelEventPublisher(Channel<DomainEvent> channel) => _channel = channel;

    public async Task PublishAsync(DomainEvent domainEvent) =>
        await _channel.Writer.WriteAsync(domainEvent);
}
