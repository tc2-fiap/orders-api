using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FiapGames.Orders.Api.Domain;

namespace FiapGames.Orders.Api.Infrastructure.Messaging;

// In-process pub/sub feeding the /api/orders/{id}/stream SSE endpoint —
// there is no persistent queue behind this, so a status published while
// nobody is subscribed is simply not observed by a later subscriber. That's
// fine here because Endpoints/OrderEndpoints.cs always reads the order's
// current status straight from the database before ever subscribing, and
// only subscribes when that read is still Pending. Safe only because
// orders-api runs a single replica (see k8s/values.yaml) — a multi-replica
// deployment would need a real backplane instead. See notes.md.
public interface IOrderStatusBroadcaster
{
    void Publish(Guid orderId, OrderStatus status);

    IAsyncEnumerable<OrderStatus> Subscribe(Guid orderId, CancellationToken cancellationToken);
}

public sealed class OrderStatusBroadcaster : IOrderStatusBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<OrderStatus>> _channels = new();

    public void Publish(Guid orderId, OrderStatus status)
    {
        var channel = _channels.GetOrAdd(orderId, static _ => Channel.CreateUnbounded<OrderStatus>());
        channel.Writer.TryWrite(status);

        if (status != OrderStatus.Pending)
        {
            channel.Writer.TryComplete();
            _channels.TryRemove(orderId, out _);
        }
    }

    public async IAsyncEnumerable<OrderStatus> Subscribe(Guid orderId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = _channels.GetOrAdd(orderId, static _ => Channel.CreateUnbounded<OrderStatus>());

        try
        {
            await foreach (var status in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return status;

                if (status != OrderStatus.Pending)
                    yield break;
            }
        }
        finally
        {
            _channels.TryRemove(orderId, out _);
        }
    }
}
