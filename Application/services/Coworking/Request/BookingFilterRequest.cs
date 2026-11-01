namespace Application.Services.Coworking.Request
{
    /// <summary>
    /// Query parameters for the paginated booking list endpoint.
    /// All filters are optional — omitting them returns all bookings.
    /// </summary>
    public class BookingFilterRequest
    {
        private const int MaxPageSize = 100;
        private int _pageSize = 20;

        // ── Pagination ─────────────────────────────────────────────────────────
        public int Page { get; set; } = 1;

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 1 : value > MaxPageSize ? MaxPageSize : value;
        }

        // ── Search ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Searches across: reference number, guest full name, guest phone.
        /// </summary>
        public string? Search { get; set; }

        // ── Status filters ─────────────────────────────────────────────────────
        /// <summary>
        /// Comma-separated booking statuses in snake_case.
        /// Example: checked_in,overstayed
        /// Accepted values: pending | confirmed | checked_in | checked_out |
        ///                  cancelled | no_show | overstayed |
        ///                  pending_student_verification
        /// </summary>
        public string? BookingStatus { get; set; }

        /// <summary>
        /// Comma-separated payment statuses in snake_case.
        /// Example: unpaid,partially_paid
        /// Accepted values: unpaid | downpayment_paid | partially_paid | fully_paid
        /// </summary>
        public string? PaymentStatus { get; set; }

        // ── Resource / package filters ─────────────────────────────────────────
        public Guid? ResourceId { get; set; }
        public Guid? ServicePackageId { get; set; }

        // ── Date range filter ──────────────────────────────────────────────────
        /// <summary>
        /// Preset range: today | this_week | this_month | this_year | custom
        /// When custom, provide DateFrom (required) and DateTo (optional, defaults to DateFrom).
        /// </summary>
        public string? DateRange { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }

        // ── Sort ───────────────────────────────────────────────────────────────
        /// <summary>
        /// Column to sort by.
        /// Accepted values: scheduled_start | scheduled_end | actual_start | actual_end | created_at
        /// Omit to use the smart default sort (unpaid + active sessions first, soonest end first).
        /// </summary>
        public string? SortBy { get; set; }

        /// <summary>Accepted values: asc (default) | desc</summary>
        public string? SortDir { get; set; }
    }
}
