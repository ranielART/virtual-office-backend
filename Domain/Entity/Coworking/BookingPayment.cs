using Domain.Common;
using Domain.Enum;

namespace Domain.Entity.Coworking
{
    public class BookingPayment : GuidBaseEntity
    {
        public Guid BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentType PaymentType { get; set; }
        public PaymentValidationStatus PaymentValidationStatus { get; set; }

        // Manual proof upload (GCash/Maya/BankTransfer)
        public string? ProofOfPaymentUrl { get; set; }

        // Guest-provided transaction reference
        public string? ReferenceNumber { get; set; }

        // Paymongo event ID — stored for idempotency checks
        public string? ExternalPaymentRef { get; set; }

        public DateTime? PaidAt { get; set; }

        // String FKs to AspNetUsers — no navigation property
        public string? ValidatedByUserId { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public string? ProcessedByUserId { get; set; }
    }
}
