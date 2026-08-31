using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FiapGames.Orders.Api.Application.Abstractions;

namespace FiapGames.Orders.Api.Infrastructure.Http;

public sealed class CatalogApiClient : ICatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogGame?> GetGameAsync(Guid gameId, string bearerToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/games/{gameId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var game = await response.Content.ReadFromJsonAsync<CatalogGameResponse>(cancellationToken);
        return game is null ? null : new CatalogGame(game.Id, game.Price);
    }

    private sealed record CatalogGameResponse(Guid Id, decimal Price);
}
