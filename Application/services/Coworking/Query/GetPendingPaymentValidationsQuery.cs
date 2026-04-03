using Application.Common.Interfaces;
using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetPendingPaymentValidationsQuery : IRequest<List<PendingPaymentValidationResponse>> { }

    public class GetPendingPaymentValidationsQueryHandler
        : IRequestHandler<GetPendingPaymentValidationsQuery, List<PendingPaymentValidationResponse>>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IStorageService _storageService;

        public GetPendingPaymentValidationsQueryHandler(ApplicationDbContext dbContext, IStorageService storageService)
        {
            _dbContext = dbContext;
            _storageService = storageService;
        }

        public async Task<List<PendingPaymentValidationResponse>> Handle(
            GetPendingPaymentValidationsQuery request, CancellationToken cancellationToken)
        {
            var payments = await _dbContext.BookingPayments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Resource)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Guest)
                .Where(p => p.PaymentValidationStatus == PaymentValidationStatus.PendingValidation)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            var responses = await Task.WhenAll(payments.Select(async p =>
            {
                var proofUrl = p.ProofOfPaymentUrl != null
                    ? await _storageService.GetSasUrlFromAbsoluteUrlAsync(p.ProofOfPaymentUrl, TimeSpan.FromMinutes(30))
                    : string.Empty;

                return new PendingPaymentValidationResponse
                {
                    PaymentId = p.Id,
                    BookingId = p.BookingId,
                    ReferenceNumber = p.Booking.ReferenceNumber,
                    GuestName = p.Booking.Guest != null
                                        ? $"{p.Booking.Guest.FirstName} {p.Booking.Guest.LastName}"
                                        : string.Empty,
                    ResourceName = p.Booking.Resource?.Name ?? "N/A",
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    PaymentType = p.PaymentType,
                    ProofOfPaymentUrl = proofUrl,
                    CreatedAt = p.CreatedAt
                };
            }));

            return responses.ToList();
        }
    }
}
