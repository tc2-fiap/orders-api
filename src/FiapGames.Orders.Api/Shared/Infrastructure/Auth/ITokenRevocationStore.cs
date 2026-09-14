namespace FiapGames.Shared.Infrastructure.Auth;

public interface ITokenRevocationStore
{
    void Revoke(string jti, DateTime expiresAtUtc);

    bool IsRevoked(string jti);
}
