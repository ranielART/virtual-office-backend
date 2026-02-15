using Application.Common.Interfaces;
using Application.Services.User.Request;
using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Persistence.Data;
using System.IdentityModel.Tokens.Jwt;

namespace Application.Services.User.Command
{
    public class LoginCommand : IRequest<LoginResponse>
    {
        public LoginRequest Request { get; set; }

        public LoginCommand(LoginRequest request)
        {
            Request = request;
        }
    }

    public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly ApplicationDbContext _dbContext;

        public LoginCommandHandler(
            UserManager<Domain.Entity.Identity.User> userManager,
            ITokenService tokenService,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _dbContext = dbContext;
        }

        public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var loginRequest = request.Request;

            // Find user by email
            var user = await _userManager.FindByEmailAsync(loginRequest.Email);
            if (user == null)
            {
                throw new InvalidOperationException("Invalid email or password");
            }

            // Check password
            var passwordValid = await _userManager.CheckPasswordAsync(user, loginRequest.Password);
            if (!passwordValid)
            {
                throw new InvalidOperationException("Invalid email or password");
            }

            var roles = await _userManager.GetRolesAsync(user);

            // 2. Your TokenService needs a SINGLE string (for now).
            // So we grab the first one. 
            string userRole = roles.FirstOrDefault() ?? "Customer";
            // Generate access token
            var accessToken = _tokenService.GenerateAccessToken(user, userRole);

            // Extract JTI (JWT ID) from the access token to link with refresh token
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(accessToken) as JwtSecurityToken;
            var jwtId = (jsonToken?.Claims.FirstOrDefault(c => c.Type == "jti")?.Value) ?? Guid.NewGuid().ToString();

            // Generate and save refresh token
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, jwtId);
            _dbContext.Set<Domain.Entity.Identity.RefreshToken>().Add(refreshToken);
            await _dbContext.SaveChangesAsync(cancellationToken);


            return new LoginResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token
            };
        }
    }
}
