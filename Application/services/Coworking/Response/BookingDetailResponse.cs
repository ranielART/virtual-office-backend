using Domain.Entity.Coworking;
using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class BookingDetailResponse
    {
        public Guid BookingId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public BookingType BookingType { get; set; }

        public BookingStatus BookingStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string GuestPhone { get; set; } = string.Empty;
        public string? GuestEmail { get; set; }
        public bool IsStudent { get; set; }
        public bool IsStudentVerified { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string SnapshotPackageName { get; set; } = string.Empty;
        public PaymentScheme SnapshotPaymentScheme { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal OverstayAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DownpaymentAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal? DailyRate { get; set; }
        public string? Notes { get; set; }
        public List<BookingPaymentResponse> Payments { get; set; } = [];
    }
}
