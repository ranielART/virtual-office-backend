using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Services.Coworking.Request;
using Domain.Entity.Coworking;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class SubmitManualPaymentProofCommand : IRequest<Unit>
    {
        public SubmitManualPaymentProofRequest Request { get; }
        public IFormFile File { get; }
        public string ProcessedByUserId { get; }

        public SubmitManualPaymentProofCommand(
            SubmitManualPaymentProofRequest request,
            IFormFile file,
            string processedByUserId)
        {
            Request = request;
            File = file;
            ProcessedByUserId = processedByUserId;
        }
    }

    public class SubmitManualPaymentProofCommandHandler
        : IRequestHandler<SubmitManualPaymentProofCommand, Unit>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IStorageService _storageService;

        public SubmitManualPaymentProofCommandHandler(
            ApplicationDbContext dbContext,
            IStorageService storageService)
        {
            _dbContext = dbContext;
            _storageService = storageService;
        }

        public async Task<Unit> Handle(
            SubmitManualPaymentProofCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            var booking = await _dbContext.Bookings
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.Id == req.BookingId, cancellationToken)
                ?? throw new NotFoundException($"Booking '{req.BookingId}' not found.");

            if (booking.BookingStatus == BookingStatus.Cancelled ||
                booking.BookingStatus == BookingStatus.NoShow)
                throw new ConflictException("Payments cannot be submitted for a booking that is cancelled or marked as no-show.");

            // Calculate current paid amount from existing DB payments before adding the new one
            var currentPaid = booking.Payments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .Sum(p => p.Amount);

            // Guard — prevent submission if booking is already fully paid
            if (currentPaid >= booking.TotalAmount)
                throw new ConflictException("This booking is already fully paid. No further payments can be submitted.");

            // Upload to Azure Blob Storage
            var fileName  = Path.GetFileName(command.File.FileName);
            var key       = $"payment-proofs/{req.BookingId}/{Guid.NewGuid()}_{fileName}";
            var proofUrl  = await _storageService.UploadFileAsync(
                                key,
                                command.File.OpenReadStream(),
                                command.File.ContentType);

            // FrontDesk physically verified the proof — immediately validated, no pending step
            var payment = new BookingPayment
            {
                Id                      = Guid.NewGuid(),
                BookingId               = req.BookingId,
                Amount                  = req.Amount,
                PaymentMethod           = PaymentMethod.ImageAttachment,
                PaymentType             = req.PaymentType,
                PaymentValidationStatus = PaymentValidationStatus.Validated,
                ProofOfPaymentUrl       = proofUrl,
                ReferenceNumber         = req.GuestProvidedReference,
                ProcessedByUserId       = command.ProcessedByUserId,
                PaidAt                  = DateTime.UtcNow,
                CreatedAt               = DateTime.UtcNow,
                UpdatedAt               = DateTime.UtcNow
            };

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
    }
}
