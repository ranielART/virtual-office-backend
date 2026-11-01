using Application.Services.Public.Command;
using Application.Common.Extensions;
using Application.Services.Public.Request;
using Application.Services.Public.Query;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
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
        public async Task<IActionResult> GetAvailablePackages([FromQuery] string? resourceType = null)
        {
            var parsedResourceType = ResourceTypeExtensions.ParseResourceType(resourceType);

            var result = await _mediator.Send(new GetAvailablePackagesQuery(parsedResourceType));
            return Ok(result);
        }

        [HttpPost("contact-us")]
        [EnableRateLimiting("contact-us")]
        public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
        {
            await _mediator.Send(new SendEmailCommand(request));
            return Ok(new { message = "Your message has been sent successfully." });
        }
    }
}