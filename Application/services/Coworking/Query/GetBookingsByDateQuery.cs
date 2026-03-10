using Application.Services.Coworking.Response;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetBookingsByDateQuery : IRequest<List<BookingSessionResponse>>
    {
        public DateOnly Date { get; }
        public GetBookingsByDateQuery(DateOnly date) => Date = date;
    }

    public class GetBookingsByDateQueryHandler
        : IRequestHandler<GetBookingsByDateQuery, List<BookingSessionResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetBookingsByDateQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<BookingSessionResponse>> Handle(
            GetBookingsByDateQuery request, CancellationToken cancellationToken)
        {
            var dateStart = request.Date.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            var dateEnd   = request.Date.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

            var bookings = await _dbContext.Bookings
                .Include(b => b.Guest)
                .Include(b => b.Resource)
                .Where(b => b.ScheduledStart >= dateStart && b.ScheduledStart <= dateEnd)
                .OrderBy(b => b.ScheduledStart)
                .ToListAsync(cancellationToken);

            return bookings.Select(b => new BookingSessionResponse
            {
                BookingId           = b.Id,
                ReferenceNumber     = b.ReferenceNumber,
                GuestName           = b.Guest != null ? $"{b.Guest.FirstName} {b.Guest.LastName}" : string.Empty,
                ResourceName        = b.Resource.Name,
                BookingStatus       = b.BookingStatus,
                PaymentStatus       = b.PaymentStatus,
                ScheduledStart      = b.ScheduledStart,
                ScheduledEnd        = b.ScheduledEnd,
                ActualStart         = b.ActualStart,
                SnapshotPackageName = b.SnapshotPackageName,
                TotalAmount         = b.TotalAmount,
                BalanceAmount       = b.BalanceAmount
            }).ToList();
        }
    }
}
