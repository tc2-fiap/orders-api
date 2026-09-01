using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Shared.Kernel.Pagination;
using FiapGames.Shared.Kernel.Results;

namespace FiapGames.Orders.Api.Application.Abstractions;

public interface IOrderService
{
    Task<Result<OrderResponse>> CreateAsync(Guid userId, CreateOrderRequest request, string bearerToken, CancellationToken cancellationToken = default);

    Task<Result<OrderResponse>> GetByIdAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<LibraryItemResponse>> GetLibraryAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default);

    Task<Result> RemoveFromLibraryAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderResponse>> GetAllOrdersAdminAsync(PagedRequest request, string? status, DateTime? from, DateTime? to, string? orderId, string? userIds, string? gameIds, decimal? minPrice, decimal? maxPrice, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OrderEventResponse>>> GetOrderEventsAdminAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderEventResponse>> GetAllOrderEventsAdminAsync(PagedRequest request, string? eventType, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
