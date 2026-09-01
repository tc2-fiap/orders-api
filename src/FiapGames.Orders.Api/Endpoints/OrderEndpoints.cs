using System.Runtime.CompilerServices;
using System.Security.Claims;
using FiapGames.Orders.Api.Application.Abstractions;
using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Domain;
using FiapGames.Orders.Api.Infrastructure.Messaging;
using FiapGames.Shared.Infrastructure.Extensions;
using FiapGames.Shared.Kernel.Pagination;
using FluentValidation;

namespace FiapGames.Orders.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();

        group.MapPost("/", async (
            CreateOrderRequest request,
            IValidator<CreateOrderRequest> validator,
            IOrderService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var userId = GetUserId(httpContext.User);
            var bearerToken = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", string.Empty);

            var result = await service.CreateAsync(userId, request, bearerToken, cancellationToken);
            return result.ToHttpResult(order => Results.Created($"/api/orders/{order.Id}", order));
        });

        group.MapGet("/admin", async ([AsParameters] PagedRequest request, IOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAllOrdersAdminAsync(request, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapGet("/{id:guid}", async (Guid id, IOrderService service, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(httpContext.User);
            var result = await service.GetByIdAsync(userId, id, cancellationToken);
            return result.ToHttpResult();
        });

        group.MapGet("/{id:guid}/stream", async (Guid id, IOrderService service, IOrderStatusBroadcaster broadcaster, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(httpContext.User);
            var result = await service.GetByIdAsync(userId, id, cancellationToken);
            if (result.IsFailure)
                return result.ToHttpResult();

            return TypedResults.ServerSentEvents(StreamOrderStatusAsync(result.Value, broadcaster, cancellationToken), eventType: "order-status");
        });

        group.MapGet("/{id:guid}/events", async (Guid id, IOrderService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetOrderEventsAdminAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapGet("/admin/events", async (
            [AsParameters] PagedRequest request,
            string? eventType,
            DateTime? from,
            DateTime? to,
            IOrderService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAllOrderEventsAdminAsync(request, eventType, from, to, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        endpoints.MapGet("/api/library", async (
            [AsParameters] PagedRequest request,
            IOrderService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(httpContext.User);
            var result = await service.GetLibraryAsync(userId, request, cancellationToken);
            return Results.Ok(result);
        }).WithTags("Library").RequireAuthorization();

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }

    // Emits the order's current status once; if it's already terminal
    // (Paid/Failed — e.g. a page refresh after the order already settled)
    // the stream ends immediately without ever touching the broadcaster.
    // Otherwise it waits on the broadcaster for the next update, bounded by
    // a safety timeout well above the simulated gateway's processing delay.
    private static async IAsyncEnumerable<OrderStatusEvent> StreamOrderStatusAsync(
        OrderResponse order,
        IOrderStatusBroadcaster broadcaster,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new OrderStatusEvent(order.Status);

        if (order.Status != OrderStatus.Pending.ToString())
            yield break;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

        var enumerator = broadcaster.Subscribe(order.Id, timeoutCts.Token).GetAsyncEnumerator(timeoutCts.Token);
        try
        {
            while (true)
            {
                var moved = false;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException)
                {
                    // Client disconnected or the safety timeout elapsed —
                    // end the stream quietly, nothing left to report.
                }

                if (!moved)
                    yield break;

                yield return new OrderStatusEvent(enumerator.Current.ToString());
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }
}
