using System.Threading.Channels;
using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;

namespace JiraFit.API.BackgroundServices;

public class WebhookChannel : IWebhookProcessorService
{
    private readonly Channel<MealInputDto> _channel;

    public WebhookChannel()
    {
        // Unbounded channel for simplicity; in prod use Bounded with DropOldest or similar.
        var options = new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        };
        _channel = Channel.CreateUnbounded<MealInputDto>(options);
    }

    public async ValueTask EnqueueWebhookPayloadAsync(MealInputDto payload, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(payload, cancellationToken);
    }

    public IAsyncEnumerable<MealInputDto> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
