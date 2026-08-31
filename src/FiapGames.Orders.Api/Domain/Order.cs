using FiapGames.Shared.Kernel.Entities;

namespace FiapGames.Orders.Api.Domain;

public class Order : Entity
{
    public Guid UserId { get; private set; }

    public Guid GameId { get; private set; }

    // Snapshotted at order time — never a live reference to CatalogAPI's
    // current price. See instructions.md §4.3.
    public decimal Price { get; private set; }

    public OrderStatus Status { get; private set; }

    private Order() { }

    public Order(Guid userId, Guid gameId, decimal price)
    {
        UserId = userId;
        GameId = gameId;
        Price = price;
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
        Touch();
        return true;
    }

    public bool MarkFailed()
    {
        if (Status != OrderStatus.Pending)
            return false;

        Status = OrderStatus.Failed;
        Touch();
        return true;
    }
}
