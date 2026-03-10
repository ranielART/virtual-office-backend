using Application.Services.Coworking.Query;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/coworking")]
    public class CoworkingController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CoworkingController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // GET api/coworking/availability?date=2026-03-15&startTime=09:00&endTime=17:00&resourceType=CoworkingSeats
        [HttpGet("availability")]
        public async Task<IActionResult> CheckAvailability(
            [FromQuery] CheckAvailabilityQuery query, CancellationToken ct)
        {
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }

        // GET api/coworking/availability/{resourceId}?estimatedHours=3
        [HttpGet("availability/{resourceId:guid}")]
        public async Task<IActionResult> CheckWalkInAvailability(
            Guid resourceId,
            [FromQuery] decimal estimatedHours,
            CancellationToken ct)
        {
            var result = await _mediator.Send(
                new CheckWalkInAvailabilityQuery { ResourceId = resourceId, EstimatedHours = estimatedHours }, ct);
            return Ok(result);
        }
    }
}
