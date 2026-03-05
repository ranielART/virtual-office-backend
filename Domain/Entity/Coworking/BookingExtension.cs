using Domain.Common;

namespace Domain.Entity.Coworking
{
    public class BookingExtension : GuidBaseEntity
    {
        public Guid BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public decimal AddedHours { get; set; }

        // Frozen from Booking.SnapshotHourlyRate at extension time
        public decimal SnapshotHourlyRate { get; set; }

        public decimal Amount { get; set; }

        // String FK to AspNetUsers
        public string? ProcessedByUserId { get; set; }

        public string? Notes { get; set; }
    }
}
