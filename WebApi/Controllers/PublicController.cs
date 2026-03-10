using Application.Services.Public.Query;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [Route("api/public")]
    [ApiController]
    public class PublicController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("resources")]
        public async Task<IActionResult> GetAvailableResources()
        {
            var result = await _mediator.Send(new GetAvailableResourcesQuery());
            return Ok(result);
        }

        [HttpGet("packages")]
        public async Task<IActionResult> GetAvailablePackages([FromQuery] ResourceType? resourceType = null)
        {
            var result = await _mediator.Send(new GetAvailablePackagesQuery(resourceType));
            return Ok(result);
        }
    }
}