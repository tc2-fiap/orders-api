namespace FiapGames.Orders.Api.Application.Dtos;

public sealed record CreateOrderRequest(IReadOnlyList<Guid> GameIds);

public sealed record OrderItemResponse(Guid GameId, decimal Price);

public sealed record OrderResponse(Guid Id, Guid UserId, IReadOnlyList<OrderItemResponse> Items, decimal TotalPrice, string Status, DateTime CreatedAtUtc)
{
    public static OrderResponse FromDomain(Domain.Order order) =>
        new(
            order.Id,
            order.UserId,
            order.Items.Select(i => new OrderItemResponse(i.GameId, i.Price)).ToList(),
            order.TotalPrice,
            order.Status.ToString(),
            order.CreatedAtUtc);
}

// One row per purchased game — an order can hold several, so the library
// is a flattened per-game view, not a per-order one.
public sealed record LibraryItemResponse(Guid GameId, Guid OrderId, DateTime PurchasedAtUtc);

// Pushed over the /stream SSE endpoint — see Endpoints/OrderEndpoints.cs.
public sealed record OrderStatusEvent(string Status);

public sealed record OrderEventResponse(Guid Id, string EventType, string Payload, DateTime OccurredAtUtc)
{
    public static OrderEventResponse FromDomain(Domain.OrderEvent orderEvent) =>
        new(orderEvent.Id, orderEvent.EventType, orderEvent.Payload, orderEvent.OccurredAtUtc);
}
