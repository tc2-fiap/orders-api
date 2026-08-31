namespace FiapGames.Shared.Infrastructure.Auth;

public interface ITokenService
{
    string GenerateToken(Guid userId, string email, string role);
}
