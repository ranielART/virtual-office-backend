using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class PendingPaymentValidationResponse
    {
        public Guid PaymentId { get; set; }
        public Guid BookingId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentType PaymentType { get; set; }
        public string ProofOfPaymentUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
