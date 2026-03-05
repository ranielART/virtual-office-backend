using Application.Services.Coworking.Command;
using Application.Services.Coworking.Query;
using Application.Services.Coworking.Request;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [Route("api/coworking/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class CoworkingAdminController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CoworkingAdminController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ── Resources ──────────────────────────────────────────────────────────

        [HttpPost("resources")]
        public async Task<IActionResult> CreateResource([FromBody] CreateCoworkingResourceRequest request)
        {
            var result = await _mediator.Send(new CreateCoworkingResourceCommand(request));
            return Ok(result);
        }

        [HttpPut("resources/{id:guid}")]
        public async Task<IActionResult> UpdateResource(Guid id, [FromBody] UpdateCoworkingResourceRequest request)
        {
            var result = await _mediator.Send(new UpdateCoworkingResourceCommand(id, request));
            return Ok(result);
        }

        [HttpPatch("resources/{id:guid}/toggle")]
        public async Task<IActionResult> ToggleResource(Guid id)
        {
            var result = await _mediator.Send(new ToggleCoworkingResourceCommand(id));
            return Ok(result);
        }

        [HttpGet("resources")]
        public async Task<IActionResult> GetAllResources(
            [FromQuery] ResourceType? resourceType = null,
            [FromQuery] bool? isActive = null)
        {
            var result = await _mediator.Send(new GetAllCoworkingResourcesQuery(resourceType, isActive));
            return Ok(result);
        }

        [HttpGet("resources/{id:guid}")]
        public async Task<IActionResult> GetResourceById(Guid id)
        {
            var result = await _mediator.Send(new GetCoworkingResourceByIdQuery(id));
            return Ok(result);
        }

        // ── Service Packages ───────────────────────────────────────────────────

        [HttpPost("packages")]
        public async Task<IActionResult> CreatePackage([FromBody] CreateServicePackageRequest request)
        {
            var result = await _mediator.Send(new CreateServicePackageCommand(request));
            return Ok(result);
        }

        [HttpPut("packages/{id:guid}")]
        public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] UpdateServicePackageRequest request)
        {
            var result = await _mediator.Send(new UpdateServicePackageCommand(id, request));
            return Ok(result);
        }

        [HttpPatch("packages/{id:guid}/toggle")]
        public async Task<IActionResult> TogglePackage(Guid id)
        {
            var result = await _mediator.Send(new ToggleServicePackageCommand(id));
            return Ok(result);
        }

        [HttpGet("packages")]
        public async Task<IActionResult> GetAllPackages(
            [FromQuery] ResourceType? resourceType = null,
            [FromQuery] bool? isActive = null)
        {
            var result = await _mediator.Send(new GetAllServicePackagesQuery(resourceType, isActive));
            return Ok(result);
        }

        [HttpGet("packages/{id:guid}")]
        public async Task<IActionResult> GetPackageById(Guid id)
        {
            var result = await _mediator.Send(new GetServicePackageByIdQuery(id));
            return Ok(result);
        }
    }
}
