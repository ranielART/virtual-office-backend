using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class ProcessWalkInTimeOutCommand : IRequest<BookingDetailResponse>
    {
        public Guid BookingId { get; }
        public DateTime? ActualEndOverride { get; }
        public string? TimeOutNotes { get; }

        public ProcessWalkInTimeOutCommand(Guid bookingId, WalkInTimeOutRequest? request = null)
        {
            BookingId         = bookingId;
            ActualEndOverride = request?.ActualEndOverride;
            TimeOutNotes      = request?.TimeOutNotes;
        }
    }

    public class ProcessWalkInTimeOutCommandHandler
        : IRequestHandler<ProcessWalkInTimeOutCommand, BookingDetailResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public ProcessWalkInTimeOutCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<BookingDetailResponse> Handle(
            ProcessWalkInTimeOutCommand command, CancellationToken cancellationToken)
        {
            var booking = await _dbContext.Bookings
                .Include(b => b.Guest)
                .Include(b => b.Payments)
                .Include(b => b.Extensions)
                .Include(b => b.Resource)
                .FirstOrDefaultAsync(b => b.Id == command.BookingId, cancellationToken)
                ?? throw new NotFoundException($"Booking '{command.BookingId}' not found.");

            if (booking.BookingStatus != BookingStatus.CheckedIn)
                throw new ConflictException("Booking is not currently checked in.");

            // Resolve effective end time
            var effectiveEnd = command.ActualEndOverride?.ToUniversalTime() ?? DateTime.UtcNow;

            // Validate override is after ActualStart
            if (effectiveEnd <= booking.ActualStart)
                throw new ConflictException("Actual end time must be after the booking's actual start time.");

            // Validate override is not in the future
            if (effectiveEnd > DateTime.UtcNow)
                throw new ConflictException("Actual end time cannot be in the future.");

            // Append notes if provided
            if (!string.IsNullOrWhiteSpace(command.TimeOutNotes))
            {
                booking.Notes = string.IsNullOrWhiteSpace(booking.Notes)
                    ? command.TimeOutNotes
                    : $"{booking.Notes}\n{command.TimeOutNotes}";
            }

            // Call domain method — sets ActualEnd, OverstayAmount, and transitions BookingStatus
            booking.CheckOut(effectiveEnd, booking.SnapshotHourlyRate);

            // Recalculate financials
            var extensionTotal = booking.Extensions.Sum(e => e.Amount);
            booking.TotalAmount = booking.BaseAmount - booking.DiscountAmount + booking.OverstayAmount + extensionTotal;

            var totalPaid = booking.Payments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .Sum(p => p.Amount);

            booking.BalanceAmount = booking.TotalAmount - totalPaid;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return MapToDetail(booking, totalPaid);
        }

        private static BookingDetailResponse MapToDetail(Domain.Entity.Coworking.Booking b, decimal totalPaid) => new()
        {
            BookingId           = b.Id,
            ReferenceNumber     = b.ReferenceNumber,
            BookingType         = b.BookingType,
            BookingStatus       = b.BookingStatus,
            PaymentStatus       = b.PaymentStatus,
            ResourceName        = b.Resource.Name,
            GuestName           = b.Guest != null ? $"{b.Guest.FirstName} {b.Guest.LastName}" : string.Empty,
            GuestPhone          = b.Guest?.Phone ?? string.Empty,
            GuestEmail          = b.Guest?.Email,
            IsStudent           = b.IsStudent,
            IsStudentVerified   = b.Guest?.IsStudentVerified ?? false,
            ScheduledStart      = b.ScheduledStart,
            ScheduledEnd        = b.ScheduledEnd,
            ActualStart         = b.ActualStart,
            ActualEnd           = b.ActualEnd,
            SnapshotPackageName = b.SnapshotPackageName,
            BaseAmount          = b.BaseAmount,
            DiscountAmount      = b.DiscountAmount,
            OverstayAmount      = b.OverstayAmount,
            TotalAmount         = b.TotalAmount,
            DownpaymentAmount   = b.DownpaymentAmount,
            BalanceAmount       = b.BalanceAmount,
            TotalPaid           = totalPaid,
            OutstandingBalance  = b.BalanceAmount,
            Notes               = b.Notes,
            Payments            = b.Payments.Select(p => new BookingPaymentResponse
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
