using Application.Services.Coworking.Command;
using Application.Services.Coworking.Query;
using Application.Services.Coworking.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/coworking/frontdesk")]
    [Authorize(Roles = "Admin,Frontdesk")]
    public class CoworkingFrontDeskController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CoworkingFrontDeskController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ── Phase 4A ───────────────────────────────────────────────────────────

        // POST api/coworking/frontdesk/time-in
        [HttpPost("time-in")]
        public async Task<IActionResult> TimeIn(
            [FromBody] WalkInTimeInRequest request, CancellationToken ct)
        {
            var frontDeskUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _mediator.Send(new ProcessWalkInTimeInCommand(request, frontDeskUserId), ct);
            return Ok(result);
        }

        // ── Phase 4B ───────────────────────────────────────────────────────────

        // POST api/coworking/frontdesk/time-out/{bookingId}
        [HttpPost("time-out/{bookingId:guid}")]
        public async Task<IActionResult> TimeOut(
            Guid bookingId,
            [FromBody] WalkInTimeOutRequest? request,
            CancellationToken ct)
        {
            var result = await _mediator.Send(new ProcessWalkInTimeOutCommand(bookingId, request), ct);
            return Ok(result);
        }

        // POST api/coworking/frontdesk/payment
        [HttpPost("payment")]
        public async Task<IActionResult> RecordPayment(
            [FromBody] ProcessPaymentRequest request, CancellationToken ct)
        {
            var processedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _mediator.Send(new ProcessPaymentCommand(request, processedByUserId), ct);
            return Ok(result);
        }

        // GET api/coworking/frontdesk/sessions/active
        [HttpGet("sessions/active")]
        public async Task<IActionResult> GetActiveSessions(CancellationToken ct)
        {
            var result = await _mediator.Send(new GetActiveSessionsQuery(), ct);
            return Ok(result);
        }

        // GET api/coworking/frontdesk/bookings?date=2026-03-15
        [HttpGet("bookings")]
        public async Task<IActionResult> GetBookingsByDate(
            [FromQuery] DateOnly date, CancellationToken ct)
        {
            var result = await _mediator.Send(new GetBookingsByDateQuery(date), ct);
            return Ok(result);
        }

        // GET api/coworking/frontdesk/bookings/{id}
        [HttpGet("bookings/{id:guid}")]
        public async Task<IActionResult> GetBookingById(Guid id, CancellationToken ct)
        {
            var result = await _mediator.Send(new GetBookingByIdQuery(id), ct);
            return Ok(result);
        }

        // ── Phase 5 ────────────────────────────────────────────────────────────

        // POST api/coworking/frontdesk/payments/submit-proof
        [HttpPost("payments/submit-proof")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitPaymentProof(
            [FromForm] SubmitManualPaymentProofRequest request,
            IFormFile file,
            CancellationToken ct)
        {
            var processedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _mediator.Send(
                new SubmitManualPaymentProofCommand(request, file, processedByUserId), ct);
            return Ok(result);
        }

        // POST api/coworking/frontdesk/payments/{paymentId}/validate
        [HttpPost("payments/{paymentId:guid}/validate")]
        public async Task<IActionResult> ValidatePayment(
            Guid paymentId,
            [FromBody] ValidateManualPaymentRequest request,
            CancellationToken ct)
        {
            var validatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _mediator.Send(
                new ValidateManualPaymentCommand(paymentId, request, validatedByUserId), ct);
            return Ok(result);
        }

        // GET api/coworking/frontdesk/payments/pending
        [HttpGet("payments/pending")]
        public async Task<IActionResult> GetPendingPayments(CancellationToken ct)
        {
            var result = await _mediator.Send(new GetPendingPaymentValidationsQuery(), ct);
            return Ok(result);
        }
    }
}
