using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    /// <summary>
    /// Lightweight booking row used in the paginated booking list.
    /// Does not include payment details — use GetBookingByIdQuery for the full record.
    /// </summary>
    public class BookingListItemResponse
    {
        public Guid BookingId { get; init; }
        public string ReferenceNumber { get; init; } = string.Empty;
        public string GuestName { get; init; } = string.Empty;
        public string GuestPhone { get; init; } = string.Empty;
        public string ResourceName { get; init; } = string.Empty;
        public BookingStatus BookingStatus { get; init; }
        public PaymentStatus PaymentStatus { get; init; }
        public PaymentScheme SnapshotPaymentScheme { get; init; }
        public DateTime ScheduledStart { get; init; }
        public DateTime ScheduledEnd { get; init; }
        public DateTime? ActualStart { get; init; }
        public DateTime? ActualEnd { get; init; }
        public string SnapshotPackageName { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public decimal BalanceAmount { get; init; }
        public bool IsStudent { get; init; }
    }
}
