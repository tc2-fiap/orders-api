using FiapGames.Orders.Api.Domain;
using FiapGames.Shared.Kernel.Pagination;
using FiapGames.Shared.Kernel.Repositories;

namespace FiapGames.Orders.Api.Application.Abstractions;

public interface IOrderRepository : IRepository<Order>
{
    Task<PagedResult<Order>> GetPaidByUserIdAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default);

    // True if the user already owns this game (Paid) or has a purchase for
    // it in flight (Pending) — a Failed order never blocks a retry.
    Task<bool> HasActiveOrderAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderEvent>> GetEventsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderEvent>> GetAllEventsAdminAsync(PagedRequest request, string? eventType, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);

    Task AddEventAsync(OrderEvent orderEvent, CancellationToken cancellationToken = default);
}
