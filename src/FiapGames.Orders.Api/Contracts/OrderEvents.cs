namespace FiapGames.Contracts;

// Fixed cross-service event contracts — see instructions.md §8. Duplicated
// verbatim (namespace + shape) into every service that publishes or
// consumes them; see notes.md 21. Must match byte-for-byte across
// orders-api, payments-api, and notifications-api.
public enum PaymentStatus
{
    Approved,
    Rejected
}

public sealed record OrderPlacedEvent(Guid OrderId, Guid UserId, IReadOnlyList<Guid> GameIds, decimal TotalPrice);

public sealed record PaymentProcessedEvent(Guid OrderId, Guid UserId, PaymentStatus Status);
