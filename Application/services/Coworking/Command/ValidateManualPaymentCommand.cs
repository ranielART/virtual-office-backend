using Application.Common.Exceptions;
using Application.Services.Coworking.Command;
using Application.Services.Coworking.Request;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class ValidateManualPaymentCommand : IRequest<Unit>
    {
        public Guid PaymentId { get; }
        public ValidateManualPaymentRequest Request { get; }
        public string ValidatedByUserId { get; }

        public ValidateManualPaymentCommand(
            Guid paymentId,
            ValidateManualPaymentRequest request,
            string validatedByUserId)
        {
            PaymentId = paymentId;
            Request = request;
            ValidatedByUserId = validatedByUserId;
        }
    }

    public class ValidateManualPaymentCommandHandler
        : IRequestHandler<ValidateManualPaymentCommand, Unit>
    {
        private readonly ApplicationDbContext _dbContext;

        public ValidateManualPaymentCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Unit> Handle(
            ValidateManualPaymentCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            var payment = await _dbContext.BookingPayments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Payments)
                .FirstOrDefaultAsync(p => p.Id == command.PaymentId, cancellationToken)
                ?? throw new NotFoundException($"Payment '{command.PaymentId}' not found.");

            if (payment.PaymentValidationStatus != PaymentValidationStatus.PendingValidation)
                throw new ConflictException("This payment has already been validated.");

            payment.ValidatedByUserId = command.ValidatedByUserId;
            payment.ValidatedAt       = DateTime.UtcNow;
            payment.UpdatedAt         = DateTime.UtcNow;

            if (req.IsApproved)
            {
                payment.PaymentValidationStatus = PaymentValidationStatus.Validated;
                payment.PaidAt                  = DateTime.UtcNow;

                // Recalculate booking PaymentStatus
                ProcessPaymentCommandHandler.RecalculatePaymentStatus(payment.Booking);

                // TODO (Phase 7): If booking is Online and this is a downpayment,
                // dispatch ConfirmDownpaymentCommand here.
            }
            else
            {
                payment.PaymentValidationStatus = PaymentValidationStatus.Rejected;
                // booking.PaymentStatus unchanged; no booking status change
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
