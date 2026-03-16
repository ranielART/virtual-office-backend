namespace Application.Services.Coworking.Response
{
    public class AdminDashboardResponse
    {
        /// <summary>Always-current state — unaffected by the period filter.</summary>
        public LiveStatsDto Live { get; set; } = new();

        /// <summary>Stats scoped to the selected period.</summary>
        public PeriodStatsDto Period { get; set; } = new();

        /// <summary>Check-in counts and billed amounts grouped by resource type within the period.</summary>
        public List<ResourceTypeBreakdownDto> ByResourceType { get; set; } = [];

        /// <summary>Check-in counts and billed amounts grouped by package type within the period.</summary>
        public List<PackageTypeBreakdownDto> ByPackageType { get; set; } = [];

        /// <summary>All-time totals — never filtered by period.</summary>
        public OverallStatsDto Overall { get; set; } = new();

        /// <summary>Last 5 bookings regardless of period.</summary>
        public List<BookingSessionResponse> RecentBookings { get; set; } = [];
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
