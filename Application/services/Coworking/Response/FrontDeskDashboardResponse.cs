using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class FrontDeskDashboardResponse
    {
        /// <summary>Always-current state — unaffected by the period filter.</summary>
        public LiveStatsDto Live { get; set; } = new();

        /// <summary>Stats scoped to the selected period (today / this week / this month / this year).</summary>
        public PeriodStatsDto Period { get; set; } = new();

        /// <summary>Check-in counts and billed amounts grouped by resource type within the period.</summary>
        public List<ResourceTypeBreakdownDto> ByResourceType { get; set; } = [];

        /// <summary>Check-in counts and billed amounts grouped by package type within the period.</summary>
        public List<PackageTypeBreakdownDto> ByPackageType { get; set; } = [];

        /// <summary>Last 5 check-ins, always ordered by most recent regardless of period.</summary>
        public List<FrontDeskBookingItemDto> RecentCheckIns { get; set; } = [];
    }

    // ── Live ──────────────────────────────────────────────────────────────────

    public class LiveStatsDto
    {
        /// <summary>Bookings currently in CheckedIn status.</summary>
        public int ActiveSessions { get; set; }

        /// <summary>Bookings currently in Overstayed status.</summary>
        public int OverstayedSessions { get; set; }

        /// <summary>Payment proofs awaiting FrontDesk/Admin validation.</summary>
        public int PendingPaymentValidations { get; set; }

        /// <summary>Active/Overstayed bookings where BalanceAmount > 0.</summary>
        public int UnpaidActiveSessions { get; set; }

        /// <summary>Sum of BalanceAmount across all active and overstayed sessions.</summary>
        public decimal OutstandingFromActive { get; set; }
    }

    // ── Period ────────────────────────────────────────────────────────────────

    public class PeriodStatsDto
    {
        /// <summary>Human-readable label, e.g. "Today", "Mar 10–16, 2026", "March 2026", "2026".</summary>
        public string PeriodLabel { get; set; } = string.Empty;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        /// <summary>Bookings whose ScheduledStart falls within the period.</summary>
        public int TotalCheckIns { get; set; }

        /// <summary>Bookings whose ActualEnd falls within the period.</summary>
        public int TotalCheckOuts { get; set; }

        /// <summary>Walk-in bookings within the period.</summary>
        public int WalkInCount { get; set; }

        /// <summary>Bookings flagged as student within the period.</summary>
        public int StudentCount { get; set; }

        /// <summary>
        /// Total of validated/no-validation-required payments whose PaidAt
        /// falls within the period — actual cash collected.
        /// </summary>
        public decimal RevenueCollected { get; set; }

        /// <summary>Sum of TotalAmount for all bookings that started in the period.</summary>
        public decimal TotalBilled { get; set; }

        /// <summary>Sum of BalanceAmount for bookings in the period that are not fully paid.</summary>
        public decimal TotalOutstanding { get; set; }

        // Payment status breakdown for bookings in the period
        public int UnpaidCount { get; set; }
        public int PartiallyPaidCount { get; set; }
        public int FullyPaidCount { get; set; }
    }

    // ── Breakdowns ────────────────────────────────────────────────────────────

    public class ResourceTypeBreakdownDto
    {
        public ResourceType ResourceType { get; set; }
        public int CheckInCount { get; set; }
        public decimal TotalBilled { get; set; }
    }

    public class PackageTypeBreakdownDto
    {
        public PackageType PackageType { get; set; }
        public int CheckInCount { get; set; }
        public decimal TotalBilled { get; set; }
    }

    // ── Recent check-ins list item ─────────────────────────────────────────────

    public class FrontDeskBookingItemDto
    {
        public Guid BookingId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string GuestPhone { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public BookingStatus BookingStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public PaymentScheme SnapshotPaymentScheme { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string SnapshotPackageName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public bool IsStudent { get; set; }
    }
}
