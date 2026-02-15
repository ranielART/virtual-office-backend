using Application.Services.User.Command;
using Application.Services.User.Request;
using Domain.Entity.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IMediator _mediator;

        public AuthController(UserManager<User> userManager, IMediator mediator)
        {
            _userManager = userManager;
            _mediator = mediator;
        }

        [HttpGet("index")]
        public IActionResult Index()
        {
            return Ok(new { message = "Hello World" });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreateUserRequest request)
        {
            try
            {
                var command = new CreateUserCommand(request);
                var result = await _mediator.Send(command);
                return CreatedAtAction(nameof(Index), result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch
            {
                return StatusCode(500, new { error = "An error occurred during registration" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var command = new LoginCommand(request);
                var result = await _mediator.Send(command);


                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,        // Prevents JS access
                    Secure = true,          // Only over HTTPS
                    SameSite = SameSiteMode.None, // Required for cross-origin Swagger testing
                    Expires = DateTime.UtcNow.AddDays(7)
                };

                Response.Cookies.Append("refreshToken", result.RefreshToken, cookieOptions);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch
            {
                return StatusCode(500, new { error = "An error occurred during login" });
            }
        }
    }
}