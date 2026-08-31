namespace FiapGames.Orders.Api.Application.Dtos;

public sealed record CreateOrderRequest(Guid GameId);

public sealed record OrderResponse(Guid Id, Guid UserId, Guid GameId, decimal Price, string Status, DateTime CreatedAtUtc)
{
    public static OrderResponse FromDomain(Domain.Order order) =>
        new(order.Id, order.UserId, order.GameId, order.Price, order.Status.ToString(), order.CreatedAtUtc);
}

public sealed record OrderEventResponse(Guid Id, string EventType, string Payload, DateTime OccurredAtUtc)
{
    public static OrderEventResponse FromDomain(Domain.OrderEvent orderEvent) =>
        new(orderEvent.Id, orderEvent.EventType, orderEvent.Payload, orderEvent.OccurredAtUtc);
}
