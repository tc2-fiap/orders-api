using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Domain;
using FiapGames.Shared.Kernel.Pagination;
using FiapGames.Shared.Kernel.Repositories;

namespace FiapGames.Orders.Api.Application.Abstractions;

public interface IOrderRepository : IRepository<Order>
{
    Task<PagedResult<LibraryItemResponse>> GetLibraryItemsByUserIdAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default);

    // Of the given game ids, the subset the user already owns (Paid) or has
    // a purchase for in flight (Pending) — a Failed order never blocks a
    // retry. Empty means the whole set is clear to order.
    Task<IReadOnlyList<Guid>> GetConflictingGameIdsAsync(Guid userId, IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderEvent>> GetEventsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderEvent>> GetAllEventsAdminAsync(PagedRequest request, string? eventType, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);

    Task AddEventAsync(OrderEvent orderEvent, CancellationToken cancellationToken = default);
}
