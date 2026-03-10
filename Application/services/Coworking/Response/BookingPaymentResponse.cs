using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class BookingPaymentResponse
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentType PaymentType { get; set; }
        public PaymentValidationStatus PaymentValidationStatus { get; set; }
        public string? ProofOfPaymentUrl { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
