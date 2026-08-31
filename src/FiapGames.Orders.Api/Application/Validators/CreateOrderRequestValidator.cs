using FiapGames.Orders.Api.Application.Dtos;
using FluentValidation;

namespace FiapGames.Orders.Api.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}
