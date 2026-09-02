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

        _repository.GetConflictingGameIdsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());
    }

    [Fact]
    public async Task CreateAsync_WhenGameExists_SnapshotsPriceAndPublishesEvent()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _catalogClient.GetGameAsync(gameId, "token", Arg.Any<CancellationToken>())
            .Returns(new CatalogGame(gameId, 29.99m));

        var result = await _sut.CreateAsync(userId, new CreateOrderRequest([gameId]), "token");

        Assert.True(result.IsSuccess);
        Assert.Equal(29.99m, result.Value.TotalPrice);
        Assert.Single(result.Value.Items);
        Assert.Equal(gameId, result.Value.Items[0].GameId);
        Assert.Equal("Pending", result.Value.Status);
        await _repository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpoint.Received(1).Publish(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithMultipleGames_SnapshotsEachPriceAndPublishesAllIds()
    {
        var userId = Guid.NewGuid();
        var gameIdA = Guid.NewGuid();
        var gameIdB = Guid.NewGuid();
        _catalogClient.GetGameAsync(gameIdA, "token", Arg.Any<CancellationToken>()).Returns(new CatalogGame(gameIdA, 29.99m));
        _catalogClient.GetGameAsync(gameIdB, "token", Arg.Any<CancellationToken>()).Returns(new CatalogGame(gameIdB, 49.13m));

        var result = await _sut.CreateAsync(userId, new CreateOrderRequest([gameIdA, gameIdB]), "token");

        Assert.True(result.IsSuccess);
        Assert.Equal(79.12m, result.Value.TotalPrice);
        Assert.Equal(2, result.Value.Items.Count);
        await _publishEndpoint.Received(1).Publish(
            Arg.Is<OrderPlacedEvent>(e => e.GameIds.Count == 2 && e.GameIds.Contains(gameIdA) && e.GameIds.Contains(gameIdB) && e.TotalPrice == 79.12m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenUserAlreadyHasAnActiveOrderForAGame_RejectsWholeOrderAndDoesNotPublish()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _repository.GetConflictingGameIdsAsync(userId, Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { gameId });

        var result = await _sut.CreateAsync(userId, new CreateOrderRequest([gameId]), "token");

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

        var result = await _sut.CreateAsync(Guid.NewGuid(), new CreateOrderRequest([gameId]), "token");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        await _publishEndpoint.DidNotReceive().Publish(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderBelongsToAnotherUser_ReturnsNotFound()
    {
        var order = new Order(Guid.NewGuid(), [(Guid.NewGuid(), 29.99m)]);
        _repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), order.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderBelongsToUser_ReturnsOrder()
    {
        var userId = Guid.NewGuid();
        var order = new Order(userId, [(Guid.NewGuid(), 29.99m)]);
        _repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.GetByIdAsync(userId, order.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(order.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetLibraryAsync_DelegatesToRepositoryLibraryProjection()
    {
        var userId = Guid.NewGuid();
        var libraryItem = new LibraryItemResponse(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        _repository.GetLibraryItemsByUserIdAsync(userId, Arg.Any<PagedRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<LibraryItemResponse>([libraryItem], 1, 1, 10));

        var result = await _sut.GetLibraryAsync(userId, new PagedRequest());

        Assert.Single(result.Items);
        Assert.Equal(libraryItem.GameId, result.Items.First().GameId);
    }

    [Fact]
    public async Task RemoveFromLibraryAsync_WhenGameNotOwned_ReturnsNotFoundAndDoesNotSave()
    {
        _repository.GetOwnedLibraryItemAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((OrderItem?)null);

        var result = await _sut.RemoveFromLibraryAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFromLibraryAsync_WhenGameOwned_MarksItemRemovedAndSaves()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var order = new Order(userId, [(gameId, 29.99m)]);
        order.MarkPaid();
        var item = order.Items[0];
        _repository.GetOwnedLibraryItemAsync(userId, gameId, Arg.Any<CancellationToken>()).Returns(item);

        var result = await _sut.RemoveFromLibraryAsync(userId, gameId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(item.RemovedFromLibraryAtUtc);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyOrdersAsync_ForwardsCallerIdAsTheOnlyUserFilter()
    {
        var userId = Guid.NewGuid();
        var order = new Order(userId, [(Guid.NewGuid(), 29.99m)]);
        _repository.GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), Arg.Any<OrderStatus?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<List<Guid>?>(), Arg.Any<List<Guid>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([order], 1, 1, 10));

        var result = await _sut.GetMyOrdersAsync(userId, new PagedRequest());

        Assert.Single(result.Items);
        Assert.Equal(userId, result.Items.Single().UserId);
        await _repository.Received(1).GetOrdersPagedAdminAsync(
            Arg.Any<PagedRequest>(), null, null, null, null,
            Arg.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] == userId),
            Arg.Is<List<Guid>>(ids => ids == null),
            null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllOrdersAdminAsync_ReturnsOrdersAcrossUsers()
    {
        var orderA = new Order(Guid.NewGuid(), [(Guid.NewGuid(), 29.99m)]);
        var orderB = new Order(Guid.NewGuid(), [(Guid.NewGuid(), 49.13m)]);
        _repository.GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), Arg.Any<OrderStatus?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<List<Guid>?>(), Arg.Any<List<Guid>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([orderA, orderB], 2, 1, 10));

        var result = await _sut.GetAllOrdersAdminAsync(new PagedRequest(), null, null, null, null, null, null, null, null);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.UserId == orderA.UserId);
        Assert.Contains(result.Items, i => i.UserId == orderB.UserId);
    }

    [Fact]
    public async Task GetAllOrdersAdminAsync_ParsesStatusFilterAndForwardsToRepository()
    {
        _repository.GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), Arg.Any<OrderStatus?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<List<Guid>?>(), Arg.Any<List<Guid>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([], 0, 1, 10));

        await _sut.GetAllOrdersAdminAsync(new PagedRequest(), "Paid", null, null, null, null, null, null, null);

        await _repository.Received(1).GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), OrderStatus.Paid, null, null, null, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllOrdersAdminAsync_ForwardsOrderIdAndPriceRangeFilters()
    {
        _repository.GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), Arg.Any<OrderStatus?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<List<Guid>?>(), Arg.Any<List<Guid>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([], 0, 1, 10));

        await _sut.GetAllOrdersAdminAsync(new PagedRequest(), null, null, null, "5d14c9b0", null, null, 10m, 200m);

        await _repository.Received(1).GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), null, null, null, "5d14c9b0", null, null, 10m, 200m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllOrdersAdminAsync_ParsesCommaSeparatedUserAndGameIds()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _repository.GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), Arg.Any<OrderStatus?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<List<Guid>?>(), Arg.Any<List<Guid>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([], 0, 1, 10));

        await _sut.GetAllOrdersAdminAsync(new PagedRequest(), null, null, null, null, $" {userId} ,", $"{gameId}", null, null);

        await _repository.Received(1).GetOrdersPagedAdminAsync(
            Arg.Any<PagedRequest>(), null, null, null, null,
            Arg.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] == userId),
            Arg.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] == gameId),
            null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllOrdersAdminAsync_WhenNameSearchMatchesNobody_ForwardsEmptyListNotNull()
    {
        _repository.GetOrdersPagedAdminAsync(Arg.Any<PagedRequest>(), Arg.Any<OrderStatus?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(), Arg.Any<List<Guid>?>(), Arg.Any<List<Guid>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([], 0, 1, 10));

        await _sut.GetAllOrdersAdminAsync(new PagedRequest(), null, null, null, null, string.Empty, null, null, null);

        await _repository.Received(1).GetOrdersPagedAdminAsync(
            Arg.Any<PagedRequest>(), null, null, null, null,
            Arg.Is<List<Guid>>(ids => ids.Count == 0),
            Arg.Is<List<Guid>>(ids => ids == null),
            null, null, Arg.Any<CancellationToken>());
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
        var order = new Order(Guid.NewGuid(), [(Guid.NewGuid(), 29.99m)]);
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
