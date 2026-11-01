using Application.Common.Models;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetBookingsQuery : IRequest<PagedResult<BookingListItemResponse>>
    {
        public BookingFilterRequest Filter { get; }
        public GetBookingsQuery(BookingFilterRequest filter) => Filter = filter;
    }

    public class GetBookingsQueryHandler
        : IRequestHandler<GetBookingsQuery, PagedResult<BookingListItemResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetBookingsQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PagedResult<BookingListItemResponse>> Handle(
            GetBookingsQuery request, CancellationToken cancellationToken)
        {
            var f = request.Filter;

            // Start with a non-tracking queryable — nothing hits the DB yet.
            // Each filter method appends a WHERE clause; EF Core composes them
            // into a single SQL statement when the query is finally executed.
            IQueryable<Domain.Entity.Coworking.Booking> query =
                _dbContext.Bookings.AsNoTracking();

            // ── Apply filters ───────────────────────────────────────────────────
            query = ApplySearch(query, f.Search);
            query = ApplyBookingStatusFilter(query, f.BookingStatus);
            query = ApplyPaymentStatusFilter(query, f.PaymentStatus);
            query = ApplyResourceFilter(query, f.ResourceId);
            query = ApplyPackageFilter(query, f.ServicePackageId);
            query = ApplyDateFilter(query, f.DateRange, f.DateFrom, f.DateTo);

            // ── Count BEFORE pagination ─────────────────────────────────────────
            // Run a separate COUNT(*) query on the same filtered IQueryable.
            // This is intentionally two DB round-trips: one for count, one for data.
            var totalCount = await query.CountAsync(cancellationToken);

            // ── Apply sort ──────────────────────────────────────────────────────
            query = ApplySort(query, f.SortBy, f.SortDir);

            // ── Paginate & project ──────────────────────────────────────────────
            // Using .Select() projection (no .Include()) — EF Core generates the
            // minimum JOIN + column set needed. No orphan columns loaded.
            var page     = Math.Max(1, f.Page);
            var pageSize = f.PageSize;

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BookingListItemResponse
                {
                    BookingId           = b.Id,
                    ReferenceNumber     = b.ReferenceNumber,
                    GuestName           = b.Guest != null
                                            ? b.Guest.FirstName + " " + b.Guest.LastName
                                            : string.Empty,
                    GuestPhone          = b.Guest != null ? b.Guest.Phone : string.Empty,
                    ResourceName        = b.Resource.Name,
                    BookingStatus         = b.BookingStatus,
                    PaymentStatus         = b.PaymentStatus,
                    SnapshotPaymentScheme = b.SnapshotPaymentScheme,
                    ScheduledStart      = b.ScheduledStart,
                    ScheduledEnd        = b.ScheduledEnd,
                    ActualStart         = b.ActualStart,
                    ActualEnd           = b.ActualEnd,
                    SnapshotPackageName = b.SnapshotPackageName,
                    TotalAmount         = b.TotalAmount,
                    BalanceAmount       = b.BalanceAmount,
                    IsStudent           = b.IsStudent
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<BookingListItemResponse>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // ── Filter helpers ──────────────────────────────────────────────────────
        // Each helper receives the current IQueryable and returns a new one with
        // an additional WHERE clause. If the filter param is absent, the original
        // query is returned unchanged. This keeps the main Handle method readable.

        private static IQueryable<Domain.Entity.Coworking.Booking> ApplySearch(
            IQueryable<Domain.Entity.Coworking.Booking> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;

            var term = search.Trim().ToLower();

            // EF Core translates ToLower().Contains() to LOWER(col) LIKE '%term%' in SQL.
            // The Guest is accessed as a LEFT JOIN — the null-check prevents errors when
            // a booking has no guest record.
            return query.Where(b =>
                b.ReferenceNumber.ToLower().Contains(term) ||
                (b.Guest != null && (
                    (b.Guest.FirstName + " " + b.Guest.LastName).ToLower().Contains(term) ||
                    b.Guest.Phone.ToLower().Contains(term)
                ))
            );
        }

        private static IQueryable<Domain.Entity.Coworking.Booking> ApplyBookingStatusFilter(
            IQueryable<Domain.Entity.Coworking.Booking> query, string? statusParam)
        {
            var statuses = ParseEnumList<BookingStatus>(statusParam);
            return statuses.Count > 0 ? query.Where(b => statuses.Contains(b.BookingStatus)) : query;
        }

        private static IQueryable<Domain.Entity.Coworking.Booking> ApplyPaymentStatusFilter(
            IQueryable<Domain.Entity.Coworking.Booking> query, string? statusParam)
        {
            var statuses = ParseEnumList<PaymentStatus>(statusParam);
            return statuses.Count > 0 ? query.Where(b => statuses.Contains(b.PaymentStatus)) : query;
        }

        private static IQueryable<Domain.Entity.Coworking.Booking> ApplyResourceFilter(
            IQueryable<Domain.Entity.Coworking.Booking> query, Guid? resourceId)
            => resourceId.HasValue ? query.Where(b => b.ResourceId == resourceId.Value) : query;

        private static IQueryable<Domain.Entity.Coworking.Booking> ApplyPackageFilter(
            IQueryable<Domain.Entity.Coworking.Booking> query, Guid? packageId)
            => packageId.HasValue ? query.Where(b => b.ServicePackageId == packageId.Value) : query;

        private static IQueryable<Domain.Entity.Coworking.Booking> ApplyDateFilter(
            IQueryable<Domain.Entity.Coworking.Booking> query,
            string? dateRange, DateOnly? dateFrom, DateOnly? dateTo)
        {
            var (from, to) = ResolveDateRange(dateRange, dateFrom, dateTo);
            if (from == DateTime.MinValue) return query; // no date filter requested

            return query.Where(b => b.ScheduledStart >= from && b.ScheduledStart <= to);
        }

        // ── Sort helpers ────────────────────────────────────────────────────────

        private static IQueryable<Domain.Entity.Coworking.Booking> ApplySort(
            IQueryable<Domain.Entity.Coworking.Booking> query, string? sortBy, string? sortDir)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
                return ApplyDefaultSort(query);

            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLower() switch
            {
                "scheduled_end"  => desc ? query.OrderByDescending(b => b.ScheduledEnd)   : query.OrderBy(b => b.ScheduledEnd),
                "actual_start"   => desc ? query.OrderByDescending(b => b.ActualStart)    : query.OrderBy(b => b.ActualStart),
                "actual_end"     => desc ? query.OrderByDescending(b => b.ActualEnd)      : query.OrderBy(b => b.ActualEnd),
                "created_at"     => desc ? query.OrderByDescending(b => b.CreatedAt)      : query.OrderBy(b => b.CreatedAt),
                _                => desc ? query.OrderByDescending(b => b.ScheduledStart) : query.OrderBy(b => b.ScheduledStart),
            };
        }

        // Sentinel used in the default sort — any value beyond real booking dates.
        // Using UTC Kind to satisfy Npgsql's timestamptz requirement.
        private static readonly DateTime FarFuture = new(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Smart default sort for the FrontDesk booking list.
        ///
        /// Key 1 — Active sessions (checked_in, overstayed) float to the top.
        /// Key 2 — Within active: unpaid/partially-paid before fully paid.
        /// Key 3 — Within active: soonest ScheduledEnd first (most urgent).
        ///          Non-active sessions all receive FarFuture here, so they tie
        ///          on this key and fall through to key 4.
        /// Key 4 — Non-active: newest ScheduledStart first (latest booking at top).
        ///
        /// This produces two behaviourally distinct sub-sorts in one SQL ORDER BY:
        ///   active group  → urgency (soonest ending first)
        ///   history group → recency (newest booking first)
        ///
        /// EF Core translates the ternaries to CASE WHEN … END in SQL — no in-memory sort.
        /// </summary>
        private static IQueryable<Domain.Entity.Coworking.Booking> ApplyDefaultSort(
            IQueryable<Domain.Entity.Coworking.Booking> query)
            => query
                .OrderBy(b =>
                    b.BookingStatus == BookingStatus.CheckedIn ||
                    b.BookingStatus == BookingStatus.Overstayed ? 0 : 1)
                .ThenBy(b =>
                    b.PaymentStatus == PaymentStatus.Unpaid ||
                    b.PaymentStatus == PaymentStatus.PartiallyPaid ? 0 : 1)
                .ThenBy(b =>
                    b.BookingStatus == BookingStatus.CheckedIn ||
                    b.BookingStatus == BookingStatus.Overstayed
                        ? b.ScheduledEnd
                        : FarFuture)
                .ThenByDescending(b => b.ScheduledStart);

        // ── Utility ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses a comma-separated string of snake_case enum names.
        /// Example: "checked_in,overstayed" → [BookingStatus.CheckedIn, BookingStatus.Overstayed]
        ///
        /// Strategy: strip underscores before parsing so "checked_in" → "checkedin"
        /// which matches "CheckedIn" case-insensitively. Invalid tokens are silently ignored.
        /// </summary>
        private static List<TEnum> ParseEnumList<TEnum>(string? param)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(param)) return [];

            return param
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s =>
                {
                    var normalized = s.Replace("_", "");
                    return Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var val)
                        ? (TEnum?)val
                        : null;
                })
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
        }

        /// <summary>
        /// Converts a date range preset or custom range to UTC DateTime bounds.
        /// Returns (DateTime.MinValue, DateTime.MaxValue) when no range is specified,
        /// which the caller treats as "no date filter".
        ///
        /// All times are converted to UTC via ToUniversalTime() as required by PostgreSQL.
        /// Week starts on Monday.
        /// </summary>
        private static (DateTime from, DateTime to) ResolveDateRange(
            string? dateRange, DateOnly? dateFrom, DateOnly? dateTo)
        {
            var now   = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);

            return dateRange?.ToLower() switch
            {
                "today"     => ToDayBounds(today),
                "this_week" => (
                    StartOfWeek(today).ToDateTime(TimeOnly.MinValue).ToUniversalTime(),
                    StartOfWeek(today).AddDays(6).ToDateTime(TimeOnly.MaxValue).ToUniversalTime()),
                "this_month" => (
                    new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month),
                        23, 59, 59, 999, DateTimeKind.Utc)),
                "this_year" => (
                    new DateTime(now.Year, 1,  1,  0,  0,  0,   DateTimeKind.Utc),
                    new DateTime(now.Year, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc)),
                "custom" when dateFrom.HasValue => (
                    dateFrom.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime(),
                    (dateTo ?? dateFrom.Value).ToDateTime(TimeOnly.MaxValue).ToUniversalTime()),
                _ => (DateTime.MinValue, DateTime.MaxValue)
            };
        }

        private static (DateTime, DateTime) ToDayBounds(DateOnly date) => (
            date.ToDateTime(TimeOnly.MinValue).ToUniversalTime(),
            date.ToDateTime(TimeOnly.MaxValue).ToUniversalTime()
        );

        private static DateOnly StartOfWeek(DateOnly date)
        {
            var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.AddDays(-diff);
        }
    }
}
