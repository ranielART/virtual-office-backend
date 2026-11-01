using Domain.Common;
using Domain.Enum;

namespace Domain.Entity.Coworking
{
    public class Booking : GuidBaseEntity
    {
        public string ReferenceNumber { get; set; } = string.Empty;
        public BookingType BookingType { get; set; }
        public BookingStatus BookingStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }

        // Resource & package FKs
        public Guid ResourceId { get; set; }
        public CoworkingResource Resource { get; set; } = null!;

        public Guid ServicePackageId { get; set; }
        public ServicePackage ServicePackage { get; set; } = null!;

        // Who: string FKs to AspNetUsers — no navigation to avoid circular deps
        public string? UserId { get; set; }           // VO Member (post-MVP)
        public string? FrontDeskUserId { get; set; }  // Staff who processed the booking

        // Schedule
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }

        public int PaxCount { get; set; } = 1;
        public bool IsStudent { get; set; }

        // Price snapshot — frozen at booking creation
        public string SnapshotPackageName { get; set; } = string.Empty;
        public decimal SnapshotPricePerUnit { get; set; }
        public decimal SnapshotHourlyRate { get; set; }
        public PaymentScheme SnapshotPaymentScheme { get; set; } = PaymentScheme.Postpaid;

        // Financials
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DownpaymentAmount { get; set; }
        public DateTime? DownpaymentPaidAt { get; set; }
        public decimal BalanceAmount { get; set; }
        public decimal OverstayAmount { get; set; }
        public decimal TotalAmount { get; set; }

        // Metadata
        public string? Notes { get; set; }
        public string? CancellationReason { get; set; }

        // Background job flags — included in initial schema to avoid mid-project migrations
        public DateTime? ReminderSentAt { get; set; }
        public DateTime? EndingReminderSentAt { get; set; }

        // Navigation — children
        public BookingGuest? Guest { get; set; }
        public ICollection<BookingPayment> Payments { get; set; } = [];
        public ICollection<BookingExtension> Extensions { get; set; } = [];

        // ── Domain state transition methods ─────────────────────────────────────
        // Handlers must use these instead of mutating fields directly.

        public void CheckIn(DateTime actualStart)
        {
            ActualStart = actualStart;
            BookingStatus = BookingStatus.CheckedIn;
            UpdatedAt = DateTime.UtcNow;
        }

        public void CheckOut(DateTime actualEnd, decimal snapshotHourlyRate)
        {
            ActualEnd = actualEnd;
            var scheduledHours = (ScheduledEnd - ScheduledStart).TotalHours;
            var actualHours = (actualEnd - ActualStart!.Value).TotalHours;
            var overstayHours = Math.Max(0, actualHours - scheduledHours);

            OverstayAmount = (decimal)overstayHours * snapshotHourlyRate;
            BookingStatus = OverstayAmount > 0 ? BookingStatus.Overstayed : BookingStatus.CheckedOut;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Cancel(string reason)
        {
            BookingStatus = BookingStatus.Cancelled;
            CancellationReason = reason;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkNoShow(string? reason = null)
        {
            BookingStatus = BookingStatus.NoShow;
            PaymentStatus = PaymentStatus.FullyPaid;
            CancellationReason = reason ?? "Marked as no-show.";
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
