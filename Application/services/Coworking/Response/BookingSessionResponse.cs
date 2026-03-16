using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class BookingSessionResponse
    {
        public Guid BookingId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public BookingStatus BookingStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public DateTime? ActualStart { get; set; }

        public DateTime? ActualEnd { get; set; }

        public string SnapshotPackageName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal BalanceAmount { get; set; }
    }
}
