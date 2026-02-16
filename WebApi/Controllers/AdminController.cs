using Application.Services.User.Command;
using Application.Services.User.Query.Admin;
using Application.Services.User.Query.User;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace WebApi.Controllers
{
    [Route("api/[controller]")]
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
            // We wrap the request in the command and send it to MediatR
            var command = new CreateAdminCommand(request);
            var result = await _mediator.Send(command);

            return Ok(result);
        }

        [HttpPut("edit-admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditAdmin(string id, [FromBody] UpdateUserRequest request)
        {
            // Merge ID (route) and Data (body) into one Command
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

        // GET: api/User/active-admins
        [HttpGet("active-admins")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllActiveAdmins()
        {
            var result = await _mediator.Send(new GetAllActiveAdminsQuery());
            return Ok(result);
        }

        // GET: api/User/archived-admins
        [HttpGet("archived-admins")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllArchivedAdmins()
        {
            var result = await _mediator.Send(new GetAllArchivedAdminsQuery());
            return Ok(result);
        }

    }
}
