using FiapGames.Shared.Kernel.Entities;

namespace FiapGames.Orders.Api.Domain;

// A per-order audit trail — appended whenever this service publishes or
// receives a purchase-flow event, storing the actual payload (not a
// summary) so an admin can inspect what was really sent/received.
public sealed class OrderEvent : Entity
{
    public Guid OrderId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredAtUtc { get; private set; }

    private OrderEvent() { }

    public OrderEvent(Guid orderId, string eventType, string payload)
    {
        OrderId = orderId;
        EventType = eventType;
        Payload = payload;
        OccurredAtUtc = DateTime.UtcNow;
    }
}
