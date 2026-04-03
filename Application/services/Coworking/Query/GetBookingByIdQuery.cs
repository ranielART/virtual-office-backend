using Application.Common.Exceptions;
using Application.Common.Interfaces;
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
        private readonly IStorageService _storageService;

        public GetBookingByIdQueryHandler(ApplicationDbContext dbContext, IStorageService storageService)
        {
            _dbContext      = dbContext;
            _storageService = storageService;
        }

        public async Task<BookingDetailResponse> Handle(
            GetBookingByIdQuery request, CancellationToken cancellationToken)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Guest)
                .Include(b => b.Payments)
                .Include(b => b.Extensions)
                .Include(b => b.Resource)
                .Include(b => b.ServicePackage)
                .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken)
                ?? throw new NotFoundException($"Booking '{request.BookingId}' not found.");

            var totalPaid = booking.Payments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .Sum(p => p.Amount);

            var paymentResponses = await Task.WhenAll(booking.Payments.Select(async p =>
            {
                var proofUrl = p.ProofOfPaymentUrl != null
                    ? await _storageService.GetSasUrlFromAbsoluteUrlAsync(p.ProofOfPaymentUrl, TimeSpan.FromMinutes(30))
                    : null;

                return new BookingPaymentResponse
                {
                    PaymentId               = p.Id,
                    Amount                  = p.Amount,
                    PaymentMethod           = p.PaymentMethod,
                    PaymentType             = p.PaymentType,
                    PaymentValidationStatus = p.PaymentValidationStatus,
                    ProofOfPaymentUrl       = proofUrl,
                    PaidAt                  = p.PaidAt,
                    CreatedAt               = p.CreatedAt
                };
            }));

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
                SnapshotPackageName     = booking.SnapshotPackageName,
                SnapshotPaymentScheme   = booking.SnapshotPaymentScheme,
                BaseAmount          = booking.BaseAmount,
                DiscountAmount      = booking.DiscountAmount,
                OverstayAmount      = booking.OverstayAmount,
                TotalAmount         = booking.TotalAmount,
                DownpaymentAmount   = booking.DownpaymentAmount,
                BalanceAmount       = booking.BalanceAmount,
                TotalPaid           = totalPaid,
                OutstandingBalance  = booking.TotalAmount - totalPaid,
                DailyRate           = booking.SnapshotPaymentScheme == Domain.Enum.PaymentScheme.Daily &&
                                      booking.ServicePackage?.DurationDays > 0
                                        ? Math.Round(booking.TotalAmount / booking.ServicePackage.DurationDays!.Value, 2)
                                        : null,
                Notes               = booking.Notes,
                Payments            = paymentResponses.ToList()
            };
        }
    }
}
