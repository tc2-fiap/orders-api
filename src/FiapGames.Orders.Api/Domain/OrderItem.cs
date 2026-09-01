using FiapGames.Shared.Kernel.Entities;

namespace FiapGames.Orders.Api.Domain;

// One row per game in a multi-item order.
public sealed class OrderItem : Entity
{
    public Guid OrderId { get; private set; }

    // Denormalized from the parent Order — needed so the DB itself can
    // enforce "a user can't buy a game they already own or have pending"
    // via a partial unique index on (UserId, GameId) below, instead of
    // relying only on an app-level check-then-insert that races under
    // concurrent requests. See OrdersDbContext's order_items config.
    public Guid UserId { get; private set; }

    public Guid GameId { get; private set; }

    // Snapshotted at order time — never a live reference to CatalogAPI's
    // current price. See instructions.md §4.3.
    public decimal Price { get; private set; }

    // Mirrors the parent Order.Status — kept in sync by Order.MarkPaid()/
    // MarkFailed() via SyncStatus(). Exists only so the partial unique
    // index above can exclude Failed items (a failed order must never
    // block a retry) — Order.Status stays the single source of truth,
    // this is a denormalized copy for the DB constraint's sake only.
    public OrderStatus Status { get; private set; }

    // Library-only concept, deliberately separate from Status: removing a
    // game from the library must never touch the underlying Order's
    // one-way Pending->Paid|Failed state (the order stays Paid, on record,
    // for audit history). Null means still in the library. Excluded from
    // the partial unique index on (UserId, GameId) alongside Failed items
    // (see OrdersDbContext), so a removed game frees itself up for
    // repurchase. See notes.md 42's "Revisit if" clause.
    public DateTime? RemovedFromLibraryAtUtc { get; private set; }

    private OrderItem() { }

    public OrderItem(Guid orderId, Guid userId, Guid gameId, decimal price)
    {
        OrderId = orderId;
        UserId = userId;
        GameId = gameId;
        Price = price;
        Status = OrderStatus.Pending;
    }

    internal void SyncStatus(OrderStatus status)
    {
        Status = status;
        Touch();
    }

    // One-way, like Order.Status transitions: once removed, stays removed.
    // Only a currently-owned (Paid) item can be removed — Pending/Failed
    // items were never in the library. Returns whether it actually
    // applied, so a repeat request is a no-op rather than an error.
    public bool RemoveFromLibrary()
    {
        if (Status != OrderStatus.Paid || RemovedFromLibraryAtUtc is not null)
            return false;

        RemovedFromLibraryAtUtc = DateTime.UtcNow;
        Touch();
        return true;
    }
}
