using System.Security.Claims;
using FiapGames.Orders.Api.Application.Abstractions;
using FiapGames.Orders.Api.Application.Dtos;
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
}
