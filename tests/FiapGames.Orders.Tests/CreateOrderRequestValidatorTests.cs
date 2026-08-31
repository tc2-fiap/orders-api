using FiapGames.Orders.Api.Application.Dtos;
using FiapGames.Orders.Api.Application.Validators;

namespace FiapGames.Orders.Tests;

public class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidGameId_Passes()
    {
        var result = _validator.Validate(new CreateOrderRequest(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyGameId_Fails()
    {
        var result = _validator.Validate(new CreateOrderRequest(Guid.Empty));

        Assert.False(result.IsValid);
    }
}
