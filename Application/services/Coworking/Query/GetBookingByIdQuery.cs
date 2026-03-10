using Application.Common.Exceptions;
using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetBookingByIdQuery : IRequest<BookingDetailResponse>
    {
        public Guid BookingId { get; }
        public GetBookingByIdQuery(Guid bookingId) => BookingId = bookingId;
    }

    public class GetBookingByIdQueryHandler
        : IRequestHandler<GetBookingByIdQuery, BookingDetailResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetBookingByIdQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<BookingDetailResponse> Handle(
            GetBookingByIdQuery request, CancellationToken cancellationToken)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Guest)
                .Include(b => b.Payments)
                .Include(b => b.Extensions)
                .Include(b => b.Resource)
                .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken)
                ?? throw new NotFoundException($"Booking '{request.BookingId}' not found.");

            var totalPaid = booking.Payments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .Sum(p => p.Amount);

            return new BookingDetailResponse
            {
                BookingId           = booking.Id,
                ReferenceNumber     = booking.ReferenceNumber,
                BookingType         = booking.BookingType,
                BookingStatus       = booking.BookingStatus,
                PaymentStatus       = booking.PaymentStatus,
                ResourceName        = booking.Resource.Name,
                GuestName           = booking.Guest != null
                                        ? $"{booking.Guest.FirstName} {booking.Guest.LastName}"
                                        : string.Empty,
                GuestPhone          = booking.Guest?.Phone ?? string.Empty,
                GuestEmail          = booking.Guest?.Email,
                IsStudent           = booking.IsStudent,
                IsStudentVerified   = booking.Guest?.IsStudentVerified ?? false,
                ScheduledStart      = booking.ScheduledStart,
                ScheduledEnd        = booking.ScheduledEnd,
                ActualStart         = booking.ActualStart,
                ActualEnd           = booking.ActualEnd,
                SnapshotPackageName = booking.SnapshotPackageName,
                BaseAmount          = booking.BaseAmount,
                DiscountAmount      = booking.DiscountAmount,
                OverstayAmount      = booking.OverstayAmount,
                TotalAmount         = booking.TotalAmount,
                DownpaymentAmount   = booking.DownpaymentAmount,
                BalanceAmount       = booking.BalanceAmount,
                TotalPaid           = totalPaid,
                OutstandingBalance  = booking.TotalAmount - totalPaid,
                Notes               = booking.Notes,
                Payments            = booking.Payments.Select(p => new BookingPaymentResponse
                {
                    PaymentId               = p.Id,
                    Amount                  = p.Amount,
                    PaymentMethod           = p.PaymentMethod,
                    PaymentType             = p.PaymentType,
                    PaymentValidationStatus = p.PaymentValidationStatus,
                    ProofOfPaymentUrl       = p.ProofOfPaymentUrl,
                    PaidAt                  = p.PaidAt,
                    CreatedAt               = p.CreatedAt
                }).ToList()
            };
        }
    }
}
