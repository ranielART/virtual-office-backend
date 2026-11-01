using Domain.Enum;

namespace Application.Services.Coworking.Request
{
    public class ProcessPaymentRequest
    {
        public Guid BookingId { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentType PaymentType { get; set; }
        public string? Notes { get; set; }
    }
}
