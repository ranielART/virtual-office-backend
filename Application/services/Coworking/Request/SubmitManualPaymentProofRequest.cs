using Domain.Enum;

namespace Application.Services.Coworking.Request
{
    public class SubmitManualPaymentProofRequest
    {
        public Guid BookingId { get; set; }
        public PaymentType PaymentType { get; set; }
        public decimal Amount { get; set; }
        public string? GuestProvidedReference { get; set; }
    }
}
