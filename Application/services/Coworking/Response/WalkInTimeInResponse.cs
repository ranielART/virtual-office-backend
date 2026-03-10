namespace Application.Services.Coworking.Response
{
    public class WalkInTimeInResponse
    {
        public Guid BookingId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public string SnapshotPackageName { get; set; } = string.Empty;
        public bool IsStudent { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
