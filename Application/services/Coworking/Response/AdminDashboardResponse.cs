namespace Application.Services.Coworking.Response
{
    public class AdminDashboardResponse
    {
        public TodayStatsDto Today { get; set; } = new();
        public OverallStatsDto Overall { get; set; } = new();
        public List<BookingSessionResponse> RecentBookings { get; set; } = new();
    }

    public class TodayStatsDto
    {
        public int TotalBookings { get; set; }
        public int ActiveSessions { get; set; }
        public int OverstayedSessions { get; set; }
        public int PendingPaymentValidations { get; set; }
        public decimal Revenue { get; set; }
    }

    public class OverallStatsDto
    {
        public int TotalResources { get; set; }
        public int ActiveResources { get; set; }
        public int TotalPackages { get; set; }
        public int ActivePackages { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
