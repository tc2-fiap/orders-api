using FiapGames.Orders.Api.Domain;

namespace FiapGames.Orders.Tests;

public class OrderTests
{
    private static Order NewOrder(params decimal[] prices) =>
        new(Guid.NewGuid(), prices.Select(p => (Guid.NewGuid(), p)));

    [Fact]
    public void Constructor_WithMultipleItems_SnapshotsEachPriceAndSumsTotal()
    {
        var order = NewOrder(29.99m, 49.13m);

        Assert.Equal(2, order.Items.Count);
        Assert.Equal(79.12m, order.TotalPrice);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.All(order.Items, item => Assert.Equal(order.Id, item.OrderId));
    }

    [Fact]
    public void MarkPaid_FromPending_TransitionsAndReturnsTrue()
    {
        var order = NewOrder(29.99m);

        var applied = order.MarkPaid();

        Assert.True(applied);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkFailed_FromPending_TransitionsAndReturnsTrue()
    {
        var order = NewOrder(49.13m);

        var applied = order.MarkFailed();

        Assert.True(applied);
        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void MarkFailed_AfterAlreadyPaid_IsNoOpAndStaysPaid()
    {
        var order = NewOrder(29.99m);
        order.MarkPaid();

        var applied = order.MarkFailed();

        Assert.False(applied);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkPaid_AfterAlreadyFailed_IsNoOpAndStaysFailed()
    {
        var order = NewOrder(49.13m);
        order.MarkFailed();

        var applied = order.MarkPaid();

        Assert.False(applied);
        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void MarkPaid_CalledTwice_SecondCallIsNoOp()
    {
        var order = NewOrder(29.99m);
        order.MarkPaid();

        var applied = order.MarkPaid();

        Assert.False(applied);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void RemoveFromLibrary_OnPaidItem_AppliesAndDoesNotTouchOrderStatus()
    {
        var order = NewOrder(29.99m);
        order.MarkPaid();
        var item = order.Items[0];

        var applied = item.RemoveFromLibrary();

        Assert.True(applied);
        Assert.NotNull(item.RemovedFromLibraryAtUtc);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(OrderStatus.Paid, item.Status);
    }

    [Fact]
    public void RemoveFromLibrary_OnPendingItem_IsNoOp()
    {
        var order = NewOrder(29.99m);

        var applied = order.Items[0].RemoveFromLibrary();

        Assert.False(applied);
        Assert.Null(order.Items[0].RemovedFromLibraryAtUtc);
    }

    [Fact]
    public void RemoveFromLibrary_CalledTwice_SecondCallIsNoOp()
    {
        var order = NewOrder(29.99m);
        order.MarkPaid();
        var item = order.Items[0];
        item.RemoveFromLibrary();
        var firstRemovedAt = item.RemovedFromLibraryAtUtc;

        var applied = item.RemoveFromLibrary();

        Assert.False(applied);
        Assert.Equal(firstRemovedAt, item.RemovedFromLibraryAtUtc);
    }
}
