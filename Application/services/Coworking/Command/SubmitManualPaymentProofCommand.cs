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
                .FirstOrDefaultAsync(b => b.Id == req.BookingId, cancellationToken)
                ?? throw new NotFoundException($"Booking '{req.BookingId}' not found.");

            if (booking.BookingStatus == BookingStatus.Cancelled ||
                booking.BookingStatus == BookingStatus.NoShow)
            {
                throw new ConflictException("Payments cannot be submitted for a booking that is cancelled or marked as no-show.");
            }

            // Upload to Azure Blob Storage
            var fileName  = Path.GetFileName(command.File.FileName);
            var key       = $"payment-proofs/{req.BookingId}/{Guid.NewGuid()}_{fileName}";
            var proofUrl  = await _storageService.UploadFileAsync(
                                key,
                                command.File.OpenReadStream(),
                                command.File.ContentType);

            var payment = new BookingPayment
            {
                Id                      = Guid.NewGuid(),
                BookingId               = req.BookingId,
                Amount                  = req.Amount,
                PaymentMethod           = PaymentMethod.ImageAttachment,
                PaymentType             = req.PaymentType,
                PaymentValidationStatus = PaymentValidationStatus.PendingValidation,
                ProofOfPaymentUrl       = proofUrl,
                ReferenceNumber         = req.GuestProvidedReference,
                ProcessedByUserId       = command.ProcessedByUserId,
                PaidAt                  = null,  // set only after FrontDesk validation
                CreatedAt               = DateTime.UtcNow,
                UpdatedAt               = DateTime.UtcNow
            };

            _dbContext.BookingPayments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
