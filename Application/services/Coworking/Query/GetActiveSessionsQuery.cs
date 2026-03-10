using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetActiveSessionsQuery : IRequest<List<BookingSessionResponse>> { }

    public class GetActiveSessionsQueryHandler
        : IRequestHandler<GetActiveSessionsQuery, List<BookingSessionResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetActiveSessionsQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<BookingSessionResponse>> Handle(
            GetActiveSessionsQuery request, CancellationToken cancellationToken)
        {
            var bookings = await _dbContext.Bookings
                .Include(b => b.Guest)
                .Include(b => b.Resource)
                .Where(b => b.BookingStatus == BookingStatus.CheckedIn)
                .OrderBy(b => b.ActualStart)
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
