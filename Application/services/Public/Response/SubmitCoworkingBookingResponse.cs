namespace Application.Services.Public.Response
{
    public class SubmitCoworkingBookingResponse
    {
        public Guid BookingId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public decimal TotalAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
