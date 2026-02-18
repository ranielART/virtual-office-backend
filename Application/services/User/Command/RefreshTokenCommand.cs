using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Services.User.Request;
using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using System.IdentityModel.Tokens.Jwt;

namespace Application.Services.User.Command
{
    public class RefreshTokenCommand : IRequest<RefreshTokenResponse>
    {
        public RefreshTokenRequest Request { get; set; }

        public RefreshTokenCommand(RefreshTokenRequest request)
        {
            Request = request;
        }
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly ApplicationDbContext _dbContext;

        public RefreshTokenCommandHandler(
            UserManager<Domain.Entity.Identity.User> userManager,
            ITokenService tokenService,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _dbContext = dbContext;
        }

        public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var refreshTokenRequest = request.Request;

            // 1. Extract claims from the expired access token
            var principal = _tokenService.GetPrincipalFromExpiredToken(refreshTokenRequest.AccessToken);
            var userId = principal.FindFirst("sub")?.Value
                ?? throw new UnauthorizedException("Invalid access token");
            var jti = principal.FindFirst("jti")?.Value
                ?? throw new UnauthorizedException("Invalid access token");

            // 2. Look up the refresh token in the database
            var storedRefreshToken = await _dbContext.Set<Domain.Entity.Identity.RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.Token == refreshTokenRequest.RefreshToken, cancellationToken);

            if (storedRefreshToken == null)
                throw new UnauthorizedException("Invalid refresh token");

            // 3. Breach detection: if this token was already used, invalidate ALL tokens for the user
            if (storedRefreshToken.Used)
            {
                var allUserTokens = await _dbContext.Set<Domain.Entity.Identity.RefreshToken>()
                    .Where(rt => rt.UserId == storedRefreshToken.UserId)
                    .ToListAsync(cancellationToken);

                foreach (var token in allUserTokens)
                {
                    token.Invalidated = true;
                    token.UpdatedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new UnauthorizedException("Potential token reuse detected. All sessions have been invalidated. Please log in again.");
            }

            // 4. Validate the refresh token
            if (storedRefreshToken.Invalidated)
                throw new UnauthorizedException("Refresh token has been revoked");

            if (storedRefreshToken.ExpiryDate < DateTime.UtcNow)
                throw new UnauthorizedException("Refresh token has expired");

            if (storedRefreshToken.UserId != userId)
                throw new UnauthorizedException("Refresh token does not belong to this user");

            if (storedRefreshToken.JwtId != jti)
                throw new UnauthorizedException("Refresh token does not match the access token");

            // 5. Mark old refresh token as used
            storedRefreshToken.Used = true;
            storedRefreshToken.UpdatedAt = DateTime.UtcNow;

            // 6. Get user and role for new access token
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new NotFoundException("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            string userRole = roles.FirstOrDefault() ?? "Customer";

            // 7. Generate new access token
            var newAccessToken = _tokenService.GenerateAccessToken(user, userRole);

            // 8. Extract JTI from new access token
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(newAccessToken) as JwtSecurityToken;
            var newJti = jsonToken?.Claims.FirstOrDefault(c => c.Type == "jti")?.Value
                ?? Guid.NewGuid().ToString();

            // 9. Generate new refresh token and save
            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, newJti);
            _dbContext.Set<Domain.Entity.Identity.RefreshToken>().Add(newRefreshToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RefreshTokenResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token
            };
        }
    }
}
