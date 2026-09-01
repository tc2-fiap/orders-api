using FiapGames.Shared.Kernel.Entities;

namespace FiapGames.Orders.Api.Domain;

public class Order : Entity
{
    public Guid UserId { get; private set; }

    private readonly List<OrderItem> _items = new();

    public IReadOnlyList<OrderItem> Items => _items;

    // Sum of each item's snapshotted price — never a live catalog re-read.
    public decimal TotalPrice => _items.Sum(i => i.Price);

    public OrderStatus Status { get; private set; }

    private Order() { }

    public Order(Guid userId, IEnumerable<(Guid GameId, decimal Price)> items)
    {
        UserId = userId;
        _items = items.Select(i => new OrderItem(Id, userId, i.GameId, i.Price)).ToList();
        Status = OrderStatus.Pending;
    }

    // One-way: Pending -> Paid | Failed only. A late or duplicated event
    // must never move an order backwards or flip a settled one — so these
    // are no-ops once the order has left Pending, not exceptions. Returns
    // whether the transition actually happened, so callers can log the
    // difference between "applied" and "ignored duplicate/late event".
    public bool MarkPaid()
    {
        if (Status != OrderStatus.Pending)
            return false;

        Status = OrderStatus.Paid;
        foreach (var item in _items)
            item.SyncStatus(Status);
        Touch();
        return true;
    }

    public bool MarkFailed()
    {
        if (Status != OrderStatus.Pending)
            return false;

        Status = OrderStatus.Failed;
        foreach (var item in _items)
            item.SyncStatus(Status);
        Touch();
        return true;
    }
}
