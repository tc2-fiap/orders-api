using FiapGames.Shared.Infrastructure.ErrorHandling;
using Microsoft.Extensions.DependencyInjection;

namespace FiapGames.Shared.Infrastructure.Extensions;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
