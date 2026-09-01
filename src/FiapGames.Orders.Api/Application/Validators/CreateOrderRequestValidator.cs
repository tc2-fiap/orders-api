using FiapGames.Orders.Api.Application.Dtos;
using FluentValidation;

namespace FiapGames.Orders.Api.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.GameIds)
            .NotEmpty()
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Duplicate game ids are not allowed in the same order.");

        RuleForEach(x => x.GameIds).NotEmpty();
    }
}
