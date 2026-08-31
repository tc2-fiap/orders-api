namespace FiapGames.Orders.Api.Application.Abstractions;

public sealed record CatalogGame(Guid Id, decimal Price);

public interface ICatalogClient
{
    Task<CatalogGame?> GetGameAsync(Guid gameId, string bearerToken, CancellationToken cancellationToken = default);
}
