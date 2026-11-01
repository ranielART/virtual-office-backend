using Application.Services.User.Command;
using Application.Services.User.Query;
using Application.Services.User.Query.User;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [Route("api/frontdesk")]
    [ApiController]
    public class FrontDeskController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FrontDeskController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("create-frontdesk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateFrontDesk([FromBody] CreateFrontDeskRequest request)
        {
            var command = new CreateFrontDeskCommand(request);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPut("edit-frontdesk/{id}")]
        [Authorize(Roles = "Admin,Frontdesk")]
        public async Task<IActionResult> EditFrontDesk(string id, [FromBody] UpdateUserRequest request)
        {
            var command = new UpdateUserCommand(id, request);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpDelete("delete-frontdesk/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFrontDesk(string id)
        {
            var result = await _mediator.Send(new DeleteUserCommand(id));
            return Ok(result);
        }

        [HttpGet("get-frontdesk/{id}")]
        [Authorize(Roles = "Admin,Frontdesk")]
        public async Task<IActionResult> GetFrontDeskById(string id)
        {
            var result = await _mediator.Send(new GetUserByIdQuery(id));
            return Ok(result);
        }

        [HttpGet("active-frontdesks")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllActiveFrontDesks()
        {
            var result = await _mediator.Send(new GetActiveUsersByRoleQuery("Frontdesk"));
            return Ok(result);
        }

        [HttpGet("archived-frontdesks")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllArchivedFrontDesks()
        {
            var result = await _mediator.Send(new GetArchivedUsersByRoleQuery("Frontdesk"));
            return Ok(result);
        }
    }
}
