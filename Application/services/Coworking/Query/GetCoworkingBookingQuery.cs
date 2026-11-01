using Application.Services.Coworking.Response;
using MediatR;

namespace Application.Services.Coworking.Query
{
    /// <summary>
    /// Public-facing query to retrieve booking details by ID.
    /// Customers use this to check their booking status.
    /// </summary>
    public class GetCoworkingBookingQuery : IRequest<BookingDetailResponse>
    {
        public Guid BookingId { get; }
        public GetCoworkingBookingQuery(Guid bookingId) => BookingId = bookingId;
    }

    /// <summary>
    /// Handler delegates to GetBookingByIdQuery for the actual retrieval.
    /// This layer provides a stable public API while allowing internal reorganization.
    /// </summary>
    public class GetCoworkingBookingQueryHandler
        : IRequestHandler<GetCoworkingBookingQuery, BookingDetailResponse>
    {
        private readonly IMediator _mediator;

        public GetCoworkingBookingQueryHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<BookingDetailResponse> Handle(
            GetCoworkingBookingQuery request, CancellationToken cancellationToken)
        {
            return await _mediator.Send(
                new GetBookingByIdQuery(request.BookingId), cancellationToken);
        }
    }
}