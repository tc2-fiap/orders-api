using System.Text.Json;
using FiapGames.Contracts;
using FiapGames.Orders.Api.Application.Abstractions;
using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Domain;
using FiapGames.Shared.Kernel.Pagination;
using FiapGames.Shared.Kernel.Results;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FiapGames.Orders.Api.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly ICatalogClient _catalogClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository repository,
        ICatalogClient catalogClient,
        IPublishEndpoint publishEndpoint,
        ILogger<OrderService> logger)
    {
        _repository = repository;
        _catalogClient = catalogClient;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<Result<OrderResponse>> CreateAsync(Guid userId, CreateOrderRequest request, string bearerToken, CancellationToken cancellationToken = default)
    {
        // Checked before the CatalogAPI round-trip since it only needs
        // local data — a user already owning this game (Paid) or having a
        // purchase in flight (Pending) is rejected here; a prior Failed
        // order never blocks a retry.
        if (await _repository.HasActiveOrderAsync(userId, request.GameId, cancellationToken))
        {
            _logger.LogWarning("Order rejected: user {UserId} already has an active order for game {GameId}", userId, request.GameId);
            return Result.Failure<OrderResponse>(Error.Conflict("You already own this game or have a pending order for it."));
        }

        // The client supplies only a GameId — price always comes from a
        // synchronous read against CatalogAPI, never from the request body.
        // See instructions.md §6.
        var game = await _catalogClient.GetGameAsync(request.GameId, bearerToken, cancellationToken);
        if (game is null)
        {
            _logger.LogWarning("Order rejected: game {GameId} not found in catalog", request.GameId);
            return Result.Failure<OrderResponse>(Error.NotFound($"Game '{request.GameId}' was not found."));
        }

        var order = new Order(userId, game.Id, game.Price);

        await _repository.AddAsync(order, cancellationToken);

        var orderPlacedEvent = new OrderPlacedEvent(order.Id, userId, order.GameId, order.Price);

        // Publish before SaveChanges: MassTransit's EF Core bus outbox
        // captures this call and flushes it in the same transaction as the
        // order insert below. Either both commit or neither does — see
        // notes.md 15 and instructions.md §10 (transactional outbox).
        await _publishEndpoint.Publish(orderPlacedEvent, cancellationToken);

        // The actual published payload, for the admin audit trail — not
        // just a summary line.
        await _repository.AddEventAsync(
            new OrderEvent(order.Id, "OrderPlacedEvent", JsonSerializer.Serialize(orderPlacedEvent)),
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} placed by {UserId} for game {GameId} at {Price}", order.Id, userId, order.GameId, order.Price);

        return Result.Success(OrderResponse.FromDomain(order));
    }

    public async Task<Result<OrderResponse>> GetByIdAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order is null || order.UserId != userId)
        {
            _logger.LogWarning("Order {OrderId} not found for user {UserId}", orderId, userId);
            return Result.Failure<OrderResponse>(Error.NotFound($"Order '{orderId}' was not found."));
        }

        return Result.Success(OrderResponse.FromDomain(order));
    }

    public async Task<PagedResult<OrderResponse>> GetLibraryAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var paged = await _repository.GetPaidByUserIdAsync(userId, request, cancellationToken);
        var items = paged.Items.Select(OrderResponse.FromDomain).ToList();
        return new PagedResult<OrderResponse>(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<PagedResult<OrderResponse>> GetAllOrdersAdminAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var paged = await _repository.GetPagedAsync(request, cancellationToken);
        var items = paged.Items.Select(OrderResponse.FromDomain).ToList();
        return new PagedResult<OrderResponse>(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<Result<IReadOnlyList<OrderEventResponse>>> GetOrderEventsAdminAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return Result.Failure<IReadOnlyList<OrderEventResponse>>(Error.NotFound($"Order '{orderId}' was not found."));
        }

        var events = await _repository.GetEventsByOrderIdAsync(orderId, cancellationToken);
        return Result.Success<IReadOnlyList<OrderEventResponse>>(events.Select(OrderEventResponse.FromDomain).ToList());
    }

    public async Task<PagedResult<OrderEventResponse>> GetAllOrderEventsAdminAsync(PagedRequest request, string? eventType, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var paged = await _repository.GetAllEventsAdminAsync(request, eventType, from, to, cancellationToken);
        var items = paged.Items.Select(OrderEventResponse.FromDomain).ToList();
        return new PagedResult<OrderEventResponse>(items, paged.TotalCount, paged.Page, paged.PageSize);
    }
}
