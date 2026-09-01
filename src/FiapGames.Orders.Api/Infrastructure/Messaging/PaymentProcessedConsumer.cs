using System.Text.Json;
using FiapGames.Contracts;
using FiapGames.Orders.Api.Domain;
using FiapGames.Orders.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FiapGames.Orders.Api.Infrastructure.Messaging;

public sealed class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly OrdersDbContext _context;
    private readonly IOrderStatusBroadcaster _broadcaster;
    private readonly ILogger<PaymentProcessedConsumer> _logger;

    public PaymentProcessedConsumer(OrdersDbContext context, IOrderStatusBroadcaster broadcaster, ILogger<PaymentProcessedConsumer> logger)
    {
        _context = context;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var message = context.Message;

        // Items must be loaded (not FindAsync) so MarkPaid/MarkFailed's
        // per-item Status mirror — see OrderItem.Status — actually persists.
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == message.OrderId, context.CancellationToken);
        if (order is null)
        {
            _logger.LogError("PaymentProcessedEvent for unknown order {OrderId} — ignoring", message.OrderId);
            return;
        }

        // Idempotent and one-way: MarkPaid/MarkFailed no-op once the order
        // has already left Pending, so a redelivered or late event can
        // never move it backwards or flip a settled order. See
        // instructions.md §10.
        var applied = message.Status == PaymentStatus.Approved ? order.MarkPaid() : order.MarkFailed();

        // The actual received payload plus the outcome, for the admin
        // audit trail — a late/duplicate event that was ignored is exactly
        // the kind of anomaly worth keeping visible, not just skipped.
        var auditPayload = JsonSerializer.Serialize(new
        {
            message,
            applied,
            resultingStatus = order.Status.ToString()
        });
        _context.OrderEvents.Add(new OrderEvent(message.OrderId, "PaymentProcessedEvent", auditPayload));

        if (!applied)
        {
            await _context.SaveChangesAsync(context.CancellationToken);

            _logger.LogWarning(
                "Ignoring {Status} for order {OrderId}: already settled as {CurrentStatus}",
                message.Status, message.OrderId, order.Status);
            return;
        }

        await _context.SaveChangesAsync(context.CancellationToken);

        _broadcaster.Publish(order.Id, order.Status);

        _logger.LogInformation("Order {OrderId} transitioned to {Status}", message.OrderId, order.Status);
    }
}
