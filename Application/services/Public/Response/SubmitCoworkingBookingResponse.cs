namespace Application.Services.Public.Response
{
    public class SubmitCoworkingBookingResponse
    {
        public string ReferenceNumber { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string ResourcePackage { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public decimal Hours { get; set; }           // 0 for flat packages
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DownpaymentAmount { get; set; }
        public bool IsStudent { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
