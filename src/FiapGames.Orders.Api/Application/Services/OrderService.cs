using System.Text.Json;
using FiapGames.Contracts;
using FiapGames.Orders.Api.Application.Abstractions;
using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Domain;
using FiapGames.Shared.Kernel.Pagination;
using FiapGames.Shared.Kernel.Results;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

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
        var gameIds = request.GameIds.Distinct().ToList();

        // Checked before any CatalogAPI round-trip since it only needs
        // local data. The whole order is rejected if any single game
        // conflicts — a user already owning it (Paid) or having a purchase
        // for it in flight (Pending); a prior Failed order never blocks a
        // retry.
        var conflictingIds = await _repository.GetConflictingGameIdsAsync(userId, gameIds, cancellationToken);
        if (conflictingIds.Count > 0)
        {
            _logger.LogWarning("Order rejected: user {UserId} already has an active order for game(s) {GameIds}", userId, conflictingIds);
            return Result.Failure<OrderResponse>(Error.Conflict(
                $"You already own or have a pending order for: {string.Join(", ", conflictingIds)}"));
        }

        // The client supplies only GameIds — each item's price always comes
        // from a synchronous read against CatalogAPI, never from the
        // request body. See instructions.md §6.
        var items = new List<(Guid GameId, decimal Price)>();
        foreach (var gameId in gameIds)
        {
            var game = await _catalogClient.GetGameAsync(gameId, bearerToken, cancellationToken);
            if (game is null)
            {
                _logger.LogWarning("Order rejected: game {GameId} not found in catalog", gameId);
                return Result.Failure<OrderResponse>(Error.NotFound($"Game '{gameId}' was not found."));
            }

            items.Add((game.Id, game.Price));
        }

        var order = new Order(userId, items);

        await _repository.AddAsync(order, cancellationToken);

        var orderPlacedEvent = new OrderPlacedEvent(order.Id, userId, order.Items.Select(i => i.GameId).ToList(), order.TotalPrice);

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

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The GetConflictingGameIdsAsync pre-check above is TOCTOU-racy
            // under concurrent requests — this is the actual backstop: the
            // partial unique index on order_items(user_id, game_id) (see
            // OrdersDbContext) rejects the insert atomically if another
            // request for the same user+game won the race in between.
            _logger.LogWarning(ex, "Order rejected for user {UserId}: a concurrent request already claimed one of {GameIds}", userId, gameIds);
            return Result.Failure<OrderResponse>(Error.Conflict(
                "You already own or have a pending order for one of the requested games."));
        }

        _logger.LogInformation("Order {OrderId} placed by {UserId} for {ItemCount} game(s) at total {TotalPrice}", order.Id, userId, order.Items.Count, order.TotalPrice);

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

    public async Task<PagedResult<LibraryItemResponse>> GetLibraryAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default) =>
        await _repository.GetLibraryItemsByUserIdAsync(userId, request, cancellationToken);

    public async Task<Result> RemoveFromLibraryAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetOwnedLibraryItemAsync(userId, gameId, cancellationToken);
        if (item is null || !item.RemoveFromLibrary())
        {
            _logger.LogWarning("Library removal rejected: user {UserId} does not own game {GameId}", userId, gameId);
            return Result.Failure(Error.NotFound($"Game '{gameId}' was not found in your library."));
        }

        await _repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Game {GameId} removed from library for user {UserId} (order {OrderId})", gameId, userId, item.OrderId);
        return Result.Success();
    }

    // Reuses the admin paged query with userIds pinned to the caller — "my
    // orders" is exactly "every order, filtered to one user", the same
    // filter the admin user-name search already resolves to (notes.md 60).
    // No new repository method or EF query needed.
    public async Task<PagedResult<OrderResponse>> GetMyOrdersAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var paged = await _repository.GetOrdersPagedAdminAsync(request, status: null, from: null, to: null, orderId: null, userIds: [userId], gameIds: null, minPrice: null, maxPrice: null, cancellationToken);
        var items = paged.Items.Select(OrderResponse.FromDomain).ToList();
        return new PagedResult<OrderResponse>(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<PagedResult<OrderResponse>> GetAllOrdersAdminAsync(PagedRequest request, string? status, DateTime? from, DateTime? to, string? orderId, string? userIds, string? gameIds, decimal? minPrice, decimal? maxPrice, CancellationToken cancellationToken = default)
    {
        OrderStatus? parsedStatus = Enum.TryParse<OrderStatus>(status, out var s) ? s : null;
        var parsedUserIds = ParseGuidList(userIds);
        var parsedGameIds = ParseGuidList(gameIds);
        var paged = await _repository.GetOrdersPagedAdminAsync(request, parsedStatus, from, to, orderId, parsedUserIds, parsedGameIds, minPrice, maxPrice, cancellationToken);
        var items = paged.Items.Select(OrderResponse.FromDomain).ToList();
        return new PagedResult<OrderResponse>(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    // A comma-separated id list resolved on the frontend from a user-name or
    // item-name search (see notes.md 60) — null means "no filter", a
    // present-but-empty result means "the name search matched nothing",
    // which must still filter the order list down to zero rows.
    private static List<Guid>? ParseGuidList(string? raw)
    {
        if (raw is null)
            return null;

        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Guid.TryParse(segment, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
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
