using Application.Services.Public.Command;
using Application.Services.Public.Request;
using Application.Services.Coworking.Query;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Controllers
{
    [Route("api/bookings")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BookingsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // POST api/bookings/coworking
        [HttpPost("coworking")]
        [Consumes("multipart/form-data")]
        [EnableRateLimiting("public-booking")]
        public async Task<IActionResult> SubmitCoworkingBooking(
            [FromForm] SubmitCoworkingBookingRequest request,
            [FromForm(Name = "payment_screenshots")] List<IFormFile>? paymentScreenshots,
            CancellationToken ct)
        {
            var result = await _mediator.Send(
                new SubmitCoworkingBookingCommand(request, paymentScreenshots ?? []), ct);
            return Ok(result);
        }

        // POST api/bookings/virtual-office
        [HttpPost("virtual-office")]
        [Consumes("multipart/form-data")]
        [EnableRateLimiting("public-booking")]
        public async Task<IActionResult> SubmitVirtualOfficeApplication(
            [FromForm] SubmitVirtualOfficeApplicationRequest request,
            [FromForm(Name = "business_documents")] List<IFormFile>? businessDocuments,
            [FromForm(Name = "down_payment_screenshots")] List<IFormFile>? downPaymentScreenshots,
            CancellationToken ct)
        {
            var result = await _mediator.Send(
                new SubmitVirtualOfficeApplicationCommand(
                    request,
                    businessDocuments ?? [],
                    downPaymentScreenshots ?? []), ct);
            return Ok(result);
        }

        [HttpGet("booking-details/{bookingId}")]
        // [EnableRateLimiting("public-booking")]
        public async Task<IActionResult> GetCoworkingBooking(
            [FromRoute] Guid bookingId,
            CancellationToken ct)
        {
            var result = await _mediator.Send(
                new GetCoworkingBookingQuery(bookingId), ct);
            return Ok(result);
        }
    }
}
