namespace FiapGames.Contracts;

// Fixed cross-service event contract — see instructions.md §8. Duplicated
// verbatim (namespace + shape) into every service that publishes or
// consumes it; see notes.md 21. Cross-cutting auth infrastructure, not a
// purchase-flow event — catalog-api/platform-api consuming this doesn't
// reopen notes.md 1's purchase-flow isolation.
public sealed record TokenRevokedEvent(string Jti, DateTime ExpiresAtUtc);
