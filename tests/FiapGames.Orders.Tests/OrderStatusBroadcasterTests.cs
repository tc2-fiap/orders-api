using FiapGames.Orders.Api.Domain;
using FiapGames.Orders.Api.Infrastructure.Messaging;

namespace FiapGames.Orders.Tests;

public class OrderStatusBroadcasterTests
{
    [Fact]
    public async Task Subscribe_ThenPublishPaid_YieldsTheUpdateAndCompletes()
    {
        var broadcaster = new OrderStatusBroadcaster();
        var orderId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var enumerator = broadcaster.Subscribe(orderId, cts.Token).GetAsyncEnumerator(cts.Token);

        // Start pulling before publishing — this is what creates the
        // channel the broadcaster's dictionary tracks. A publish that
        // arrived before anyone ever subscribed would have nothing to
        // deliver to, same as production: the SSE endpoint always
        // subscribes (for a still-Pending order) before any update can
        // occur.
        var moveNextTask = enumerator.MoveNextAsync();
        broadcaster.Publish(orderId, OrderStatus.Paid);

        Assert.True(await moveNextTask);
        Assert.Equal(OrderStatus.Paid, enumerator.Current);
        Assert.False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task Subscribe_WithNoPublish_NeverCompletesUntilCancelled()
    {
        var broadcaster = new OrderStatusBroadcaster();
        var orderId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var enumerator = broadcaster.Subscribe(orderId, cts.Token).GetAsyncEnumerator(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
    }

    [Fact]
    public void Publish_WithoutAnySubscriber_DoesNotThrow()
    {
        var broadcaster = new OrderStatusBroadcaster();

        var exception = Record.Exception(() => broadcaster.Publish(Guid.NewGuid(), OrderStatus.Failed));

        Assert.Null(exception);
    }
}
