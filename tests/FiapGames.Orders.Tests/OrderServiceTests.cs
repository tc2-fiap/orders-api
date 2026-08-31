using FiapGames.Contracts;
using FiapGames.Orders.Api.Application.Abstractions;
using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Application.Services;
using FiapGames.Orders.Api.Domain;
using FiapGames.Shared.Kernel.Pagination;
using FiapGames.Shared.Kernel.Results;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FiapGames.Orders.Tests;

public class OrderServiceTests
{
    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();
    private readonly ICatalogClient _catalogClient = Substitute.For<ICatalogClient>();
    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        var logger = Substitute.For<ILogger<OrderService>>();
        _sut = new OrderService(_repository, _catalogClient, _publishEndpoint, logger);
    }

    [Fact]
    public async Task CreateAsync_WhenGameExists_SnapshotsPriceAndPublishesEvent()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _catalogClient.GetGameAsync(gameId, "token", Arg.Any<CancellationToken>())
            .Returns(new CatalogGame(gameId, 29.99m));

        var result = await _sut.CreateAsync(userId, new CreateOrderRequest(gameId), "token");

        Assert.True(result.IsSuccess);
        Assert.Equal(29.99m, result.Value.Price);
        Assert.Equal("Pending", result.Value.Status);
        await _repository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpoint.Received(1).Publish(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenUserAlreadyHasAnActiveOrderForGame_ReturnsConflictAndDoesNotPublish()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _repository.HasActiveOrderAsync(userId, gameId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.CreateAsync(userId, new CreateOrderRequest(gameId), "token");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        await _catalogClient.DidNotReceive().GetGameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _publishEndpoint.DidNotReceive().Publish(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenGameDoesNotExist_ReturnsNotFoundAndDoesNotPublish()
    {
        var gameId = Guid.NewGuid();
        _catalogClient.GetGameAsync(gameId, "token", Arg.Any<CancellationToken>()).Returns((CatalogGame?)null);

        var result = await _sut.CreateAsync(Guid.NewGuid(), new CreateOrderRequest(gameId), "token");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        await _publishEndpoint.DidNotReceive().Publish(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderBelongsToAnotherUser_ReturnsNotFound()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 29.99m);
        _repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), order.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderBelongsToUser_ReturnsOrder()
    {
        var userId = Guid.NewGuid();
        var order = new Order(userId, Guid.NewGuid(), 29.99m);
        _repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.GetByIdAsync(userId, order.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(order.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetLibraryAsync_MapsPaidOrdersToResponses()
    {
        var userId = Guid.NewGuid();
        var order = new Order(userId, Guid.NewGuid(), 29.99m);
        order.MarkPaid();
        _repository.GetPaidByUserIdAsync(userId, Arg.Any<PagedRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([order], 1, 1, 10));

        var result = await _sut.GetLibraryAsync(userId, new PagedRequest());

        Assert.Single(result.Items);
        Assert.Equal("Paid", result.Items.First().Status);
    }

    [Fact]
    public async Task GetAllOrdersAdminAsync_ReturnsOrdersAcrossUsers()
    {
        var orderA = new Order(Guid.NewGuid(), Guid.NewGuid(), 29.99m);
        var orderB = new Order(Guid.NewGuid(), Guid.NewGuid(), 49.13m);
        _repository.GetPagedAsync(Arg.Any<PagedRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([orderA, orderB], 2, 1, 10));

        var result = await _sut.GetAllOrdersAdminAsync(new PagedRequest());

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.UserId == orderA.UserId);
        Assert.Contains(result.Items, i => i.UserId == orderB.UserId);
    }

    [Fact]
    public async Task GetOrderEventsAdminAsync_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var result = await _sut.GetOrderEventsAdminAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task GetOrderEventsAdminAsync_WhenOrderExists_ReturnsItsEvents()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 29.99m);
        _repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var orderEvent = new OrderEvent(order.Id, "OrderPlacedEvent", "{}");
        _repository.GetEventsByOrderIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(new List<OrderEvent> { orderEvent });

        var result = await _sut.GetOrderEventsAdminAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("OrderPlacedEvent", result.Value[0].EventType);
    }

    [Fact]
    public async Task GetAllOrderEventsAdminAsync_ReturnsEventsAcrossOrders()
    {
        var eventA = new OrderEvent(Guid.NewGuid(), "OrderPlacedEvent", "{}");
        var eventB = new OrderEvent(Guid.NewGuid(), "PaymentProcessedEvent", "{}");
        _repository.GetAllEventsAdminAsync(Arg.Any<PagedRequest>(), null, null, null, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OrderEvent>([eventA, eventB], 2, 1, 10));

        var result = await _sut.GetAllOrderEventsAdminAsync(new PagedRequest(), null, null, null);

        Assert.Equal(2, result.Items.Count);
    }
}
