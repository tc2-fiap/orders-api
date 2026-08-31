using FiapGames.Orders.Api.Application.Abstractions;
using FiapGames.Orders.Api.Domain;
using FiapGames.Shared.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace FiapGames.Orders.Api.Infrastructure.Persistence;

public sealed class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public OrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<PagedResult<Order>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.OrderBy(o => o.CreatedAtUtc);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query.Skip(request.Skip).Take(request.PageSize ?? 10).ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, totalCount, request.Page ?? 1, request.PageSize ?? 10);
    }

    public async Task<PagedResult<Order>> GetPaidByUserIdAsync(Guid userId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Where(o => o.UserId == userId && o.Status == OrderStatus.Paid)
            .OrderBy(o => o.CreatedAtUtc);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query.Skip(request.Skip).Take(request.PageSize ?? 10).ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, totalCount, request.Page ?? 1, request.PageSize ?? 10);
    }

    public Task<bool> HasActiveOrderAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        _context.Orders.AnyAsync(o => o.UserId == userId && o.GameId == gameId && o.Status != OrderStatus.Failed, cancellationToken);

    public async Task<IReadOnlyList<OrderEvent>> GetEventsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await _context.OrderEvents
            .Where(e => e.OrderId == orderId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<OrderEvent>> GetAllEventsAdminAsync(PagedRequest request, string? eventType, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var query = _context.OrderEvents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType);
        if (from.HasValue)
            query = query.Where(e => e.OccurredAtUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.OccurredAtUtc <= to.Value);

        query = query.OrderByDescending(e => e.OccurredAtUtc);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query.Skip(request.Skip).Take(request.PageSize ?? 10).ToListAsync(cancellationToken);

        return new PagedResult<OrderEvent>(items, totalCount, request.Page ?? 1, request.PageSize ?? 10);
    }

    public Task AddEventAsync(OrderEvent orderEvent, CancellationToken cancellationToken = default)
    {
        _context.OrderEvents.Add(orderEvent);
        return Task.CompletedTask;
    }

    public Task AddAsync(Order entity, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Order entity) => _context.Orders.Update(entity);

    public void Remove(Order entity) => _context.Orders.Remove(entity);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken) >= 0;
}
