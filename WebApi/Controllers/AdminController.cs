using Application.Services.User.Command;
using Application.Services.User.Query;
using Application.Services.User.Query.User;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [Route("api/admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("create-admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest request)
        {
            var command = new CreateAdminCommand(request);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPut("edit-admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditAdmin(string id, [FromBody] UpdateUserRequest request)
        {
            var command = new UpdateUserCommand(id, request);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpDelete("delete-admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAdmin(string id)
        {
            var result = await _mediator.Send(new DeleteUserCommand(id));
            return Ok(result);
        }

        [HttpGet("get-admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminById(string id)
        {
            var result = await _mediator.Send(new GetUserByIdQuery(id));
            return Ok(result);
        }

        [HttpGet("active-admins")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllActiveAdmins()
        {
            var result = await _mediator.Send(new GetActiveUsersByRoleQuery("Admin"));
            return Ok(result);
        }

        [HttpGet("archived-admins")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllArchivedAdmins()
        {
            var result = await _mediator.Send(new GetArchivedUsersByRoleQuery("Admin"));
            return Ok(result);
        }

        [HttpPost("reset-password/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(string userId, [FromBody] ResetPasswordRequest request)
        {
            var command = new ResetPasswordCommand(userId, request);
            await _mediator.Send(command);
            return Ok(new { message = "Password reset successfully. The user will be required to change their password on next login." });
        }
    }
}
