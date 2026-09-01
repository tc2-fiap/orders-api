using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Application.Validators;

namespace FiapGames.Orders.Tests;

public class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    [Fact]
    public void Validate_WithOneGameId_Passes()
    {
        var result = _validator.Validate(new CreateOrderRequest([Guid.NewGuid()]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithMultipleDistinctGameIds_Passes()
    {
        var result = _validator.Validate(new CreateOrderRequest([Guid.NewGuid(), Guid.NewGuid()]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyList_Fails()
    {
        var result = _validator.Validate(new CreateOrderRequest([]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyGameId_Fails()
    {
        var result = _validator.Validate(new CreateOrderRequest([Guid.Empty]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithDuplicateGameIds_Fails()
    {
        var gameId = Guid.NewGuid();

        var result = _validator.Validate(new CreateOrderRequest([gameId, gameId]));

        Assert.False(result.IsValid);
    }
}
