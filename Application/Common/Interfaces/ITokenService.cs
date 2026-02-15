using Domain.Entity.Identity;

namespace Application.Common.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user, string role);
        Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string jwtId);
    }
}
