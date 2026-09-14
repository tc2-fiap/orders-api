using FiapGames.Contracts;
using FiapGames.Shared.Infrastructure.Auth;
using MassTransit;

namespace FiapGames.Orders.Api.Infrastructure.Messaging;

public sealed class TokenRevokedConsumer : IConsumer<TokenRevokedEvent>
{
    private readonly ITokenRevocationStore _store;

    public TokenRevokedConsumer(ITokenRevocationStore store)
    {
        _store = store;
    }

    public Task Consume(ConsumeContext<TokenRevokedEvent> context)
    {
        _store.Revoke(context.Message.Jti, context.Message.ExpiresAtUtc);
        return Task.CompletedTask;
    }
}
