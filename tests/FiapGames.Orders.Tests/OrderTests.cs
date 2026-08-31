using FiapGames.Orders.Api.Domain;

namespace FiapGames.Orders.Tests;

public class OrderTests
{
    [Fact]
    public void MarkPaid_FromPending_TransitionsAndReturnsTrue()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 29.99m);

        var applied = order.MarkPaid();

        Assert.True(applied);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkFailed_FromPending_TransitionsAndReturnsTrue()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 49.13m);

        var applied = order.MarkFailed();

        Assert.True(applied);
        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void MarkFailed_AfterAlreadyPaid_IsNoOpAndStaysPaid()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 29.99m);
        order.MarkPaid();

        var applied = order.MarkFailed();

        Assert.False(applied);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkPaid_AfterAlreadyFailed_IsNoOpAndStaysFailed()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 49.13m);
        order.MarkFailed();

        var applied = order.MarkPaid();

        Assert.False(applied);
        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void MarkPaid_CalledTwice_SecondCallIsNoOp()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 29.99m);
        order.MarkPaid();

        var applied = order.MarkPaid();

        Assert.False(applied);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }
}
