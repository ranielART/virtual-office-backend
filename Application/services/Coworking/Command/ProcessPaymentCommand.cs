using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Domain.Entity.Coworking;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class ProcessPaymentCommand : IRequest<Unit>
    {
        public ProcessPaymentRequest Request { get; }
        public string ProcessedByUserId { get; }

        public ProcessPaymentCommand(ProcessPaymentRequest request, string processedByUserId)
        {
            Request = request;
            ProcessedByUserId = processedByUserId;
        }
    }

    public class ProcessPaymentCommandHandler
        : IRequestHandler<ProcessPaymentCommand, Unit>
    {
        private readonly ApplicationDbContext _dbContext;

        public ProcessPaymentCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Unit> Handle(
            ProcessPaymentCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            var booking = await _dbContext.Bookings
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.Id == req.BookingId, cancellationToken)
                ?? throw new NotFoundException($"Booking '{req.BookingId}' not found.");

            // Calculate current paid amount from existing DB payments before adding the new one
            var currentPaid = booking.Payments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .Sum(p => p.Amount);

            // Guard — prevent submission if booking is already fully paid
            if (currentPaid >= booking.TotalAmount)
                throw new ConflictException("This booking is already fully paid. No further payments can be submitted.");

            // Guard — only Cash accepted in this phase
            switch (req.PaymentMethod)
            {
                case PaymentMethod.ImageAttachment:
                    throw new ConflictException(
                        "Use the proof upload endpoint (POST frontdesk/payments/submit-proof) for image-based payments.");
                case PaymentMethod.Card:
                    throw new ConflictException("Card payments are not yet supported.");
                case PaymentMethod.GCash:
                case PaymentMethod.Maya:
                case PaymentMethod.BankTransfer:
                    throw new ConflictException(
                        "Use the proof upload endpoint (POST frontdesk/payments/submit-proof) for this payment method.");
                case PaymentMethod.Paymongo:
                    throw new ConflictException("Paymongo integration is not yet available.");
            }

            var payment = new BookingPayment
            {
                Id                      = Guid.NewGuid(),
                BookingId               = req.BookingId,
                Amount                  = req.Amount,
                PaymentMethod           = PaymentMethod.Cash,
                PaymentType             = req.PaymentType,
                PaymentValidationStatus = PaymentValidationStatus.NotRequired,
                PaidAt                  = DateTime.UtcNow,
                ProcessedByUserId       = command.ProcessedByUserId,
                CreatedAt               = DateTime.UtcNow,
                UpdatedAt               = DateTime.UtcNow
            };

            if (!string.IsNullOrWhiteSpace(req.Notes))
                payment.ReferenceNumber = req.Notes;

            _dbContext.BookingPayments.Add(payment);

            // Calculate new totals explicitly — do NOT use booking.Payments collection
            // after Add() since EF Core relationship fixup may cause double-counting
            var newTotalPaid = currentPaid + req.Amount;
            booking.BalanceAmount = booking.TotalAmount - newTotalPaid;
            booking.PaymentStatus = newTotalPaid >= booking.TotalAmount
                ? PaymentStatus.FullyPaid
                : newTotalPaid > 0
                    ? PaymentStatus.PartiallyPaid
                    : PaymentStatus.Unpaid;

            // Resolve Overstayed → CheckedOut once fully paid
            if (booking.PaymentStatus == PaymentStatus.FullyPaid &&
                booking.BookingStatus == BookingStatus.Overstayed)
            {
                booking.BookingStatus = BookingStatus.CheckedOut;
                booking.UpdatedAt     = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }

        internal static void RecalculatePaymentStatus(Domain.Entity.Coworking.Booking booking)
        {
            var totalValidatedPaid = booking.Payments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .Sum(p => p.Amount);

            booking.PaymentStatus = totalValidatedPaid >= booking.TotalAmount
                ? PaymentStatus.FullyPaid
                : totalValidatedPaid > 0
                    ? PaymentStatus.PartiallyPaid
                    : PaymentStatus.Unpaid;

            // Resolve Overstayed → CheckedOut once fully paid
            if (booking.PaymentStatus == PaymentStatus.FullyPaid &&
                booking.BookingStatus == BookingStatus.Overstayed)
            {
                booking.BookingStatus = BookingStatus.CheckedOut;
                booking.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
