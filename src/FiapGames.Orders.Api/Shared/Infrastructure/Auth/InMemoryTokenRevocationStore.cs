using System.Collections.Concurrent;

namespace FiapGames.Shared.Infrastructure.Auth;

// In-memory, per-pod — not persisted. A pod restart forgets active
// revocations, but every entry is swept at the token's own natural expiry
// anyway (<= Jwt:ExpiryMinutes), so the exposure window after a restart is
// bounded to whatever's left of that token's lifetime, never indefinite.
// See notes.md for the fuller tradeoff writeup.
public sealed class InMemoryTokenRevocationStore : ITokenRevocationStore
{
    private readonly ConcurrentDictionary<string, DateTime> _revoked = new();

    public void Revoke(string jti, DateTime expiresAtUtc) => _revoked[jti] = expiresAtUtc;

    public bool IsRevoked(string jti)
    {
        if (!_revoked.TryGetValue(jti, out var expiresAtUtc))
            return false;

        // Opportunistic sweep instead of a background timer — keeps the
        // store from growing unbounded without needing a hosted service.
        if (expiresAtUtc <= DateTime.UtcNow)
        {
            _revoked.TryRemove(jti, out _);
            return false;
        }

        return true;
    }
}
