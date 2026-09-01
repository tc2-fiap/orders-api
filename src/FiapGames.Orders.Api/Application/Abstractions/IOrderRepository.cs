using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Domain;
using FiapGames.Shared.Kernel.Pagination;
using FiapGames.Shared.Kernel.Repositories;

namespace FiapGames.Orders.Api.Application.Abstractions;

public interface IOrderRepository : IRepository<Order>
{
    Task<PagedResult<LibraryItemResponse>> GetLibraryItemsByUserIdAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default);

    // The tracked OrderItem behind one library entry — still Paid and not
    // already removed — so the caller can mutate it (RemoveFromLibrary)
    // and have SaveChangesAsync persist the change. Null if the user
    // doesn't currently own that game in their library.
    Task<OrderItem?> GetOwnedLibraryItemAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);

    // Of the given game ids, the subset the user already owns (Paid) or has
    // a purchase for in flight (Pending) — a Failed order never blocks a
    // retry. Empty means the whole set is clear to order.
    Task<IReadOnlyList<Guid>> GetConflictingGameIdsAsync(Guid userId, IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderEvent>> GetEventsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderEvent>> GetAllEventsAdminAsync(PagedRequest request, string? eventType, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);

    Task<PagedResult<Order>> GetOrdersPagedAdminAsync(PagedRequest request, OrderStatus? status, DateTime? from, DateTime? to, string? orderId, List<Guid>? userIds, List<Guid>? gameIds, decimal? minPrice, decimal? maxPrice, CancellationToken cancellationToken = default);

    Task AddEventAsync(OrderEvent orderEvent, CancellationToken cancellationToken = default);
}
