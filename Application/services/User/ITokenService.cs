using Domain.Entity.Identity;

namespace Application.services.User
{
    public interface ITokenService
    {
        string GenerateAccessToken(Domain.Entity.Identity.User user);
        Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string jwtId);
    }
}
