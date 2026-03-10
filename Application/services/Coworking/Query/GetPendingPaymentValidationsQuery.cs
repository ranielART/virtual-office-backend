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

        public GetPendingPaymentValidationsQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
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

            return payments.Select(p => new PendingPaymentValidationResponse
            {
                PaymentId        = p.Id,
                BookingId        = p.BookingId,
                ReferenceNumber  = p.Booking.ReferenceNumber,
                GuestName        = p.Booking.Guest != null
                                    ? $"{p.Booking.Guest.FirstName} {p.Booking.Guest.LastName}"
                                    : string.Empty,
                ResourceName     = p.Booking.Resource.Name,
                Amount           = p.Amount,
                PaymentMethod    = p.PaymentMethod,
                PaymentType      = p.PaymentType,
                ProofOfPaymentUrl = p.ProofOfPaymentUrl ?? string.Empty,
                CreatedAt        = p.CreatedAt
            }).ToList();
        }
    }
}
