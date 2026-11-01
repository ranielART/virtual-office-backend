using Domain.Enum;

namespace Application.Services.Coworking.Request
{
    public class WalkInTimeOutWithPaymentRequest
    {
        // ── Time-out fields ────────────────────────────────────────────────────

        /// <summary>
        /// Override the actual checkout time. Use when FrontDesk forgot to process time-out
        /// and the guest had already left earlier. Must be in the past and after ActualStart.
        /// When omitted, DateTime.UtcNow is used.
        /// </summary>
        public DateTime? ActualEndOverride { get; set; }

        /// <summary>
        /// Optional notes explaining a late or corrected time-out.
        /// </summary>
        public string? TimeOutNotes { get; set; }

        // ── Payment fields ─────────────────────────────────────────────────────

        /// <summary>
        /// Payment method used by the guest. Cash records immediately.
        /// GCash / Maya / BankTransfer / ImageAttachment require a proof file.
        /// </summary>
        public PaymentMethod PaymentMethod { get; set; }

        /// <summary>
        /// Amount paid by the guest at time-out.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Payment type — typically Balance for walk-in time-out settlements.
        /// </summary>
        public PaymentType PaymentType { get; set; }

        /// <summary>
        /// Optional payment notes or reference for cash payments.
        /// </summary>
        public string? PaymentNotes { get; set; }

        /// <summary>
        /// Reference number provided by the guest (e.g. GCash ref no.).
        /// Required for non-cash payments.
        /// </summary>
        public string? GuestProvidedReference { get; set; }
    }
}
