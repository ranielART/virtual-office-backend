using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetAdminDashboardQuery : IRequest<AdminDashboardResponse>
    {
        /// <summary>
        /// Accepted values: "today" | "this_week" | "this_month" | "this_year"
        /// Defaults to "today" when null/empty.
        /// </summary>
        public string Period { get; }

        public GetAdminDashboardQuery(string? period = null)
        {
            Period = period?.Trim().ToLowerInvariant() ?? "today";
        }
    }

    public class GetAdminDashboardQueryHandler
        : IRequestHandler<GetAdminDashboardQuery, AdminDashboardResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetAdminDashboardQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<AdminDashboardResponse> Handle(
            GetAdminDashboardQuery request, CancellationToken cancellationToken)
        {
            var (periodStart, periodEnd, periodLabel) = ResolvePeriod(request.Period);

            // ── Live stats (no date filter — always current) ──────────────────

            var activeSessions = await _dbContext.Bookings
                .Where(b => b.BookingStatus == BookingStatus.CheckedIn ||
                            b.BookingStatus == BookingStatus.Overstayed)
                .Select(b => new { b.BookingStatus, b.BalanceAmount })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var pendingValidations = await _dbContext.BookingPayments
                .CountAsync(p => p.PaymentValidationStatus == PaymentValidationStatus.PendingValidation,
                    cancellationToken);

            var live = new LiveStatsDto
            {
                ActiveSessions            = activeSessions.Count(s => s.BookingStatus == BookingStatus.CheckedIn),
                OverstayedSessions        = activeSessions.Count(s => s.BookingStatus == BookingStatus.Overstayed),
                PendingPaymentValidations = pendingValidations,
                UnpaidActiveSessions      = activeSessions.Count(s => s.BalanceAmount > 0),
                OutstandingFromActive     = activeSessions.Sum(s => s.BalanceAmount)
            };

            // ── Period bookings (single projection — one round-trip) ──────────

            var periodBookings = await _dbContext.Bookings
                .Where(b => b.ScheduledStart >= periodStart && b.ScheduledStart < periodEnd)
                .Select(b => new
                {
                    b.Id,
                    b.BookingStatus,
                    b.PaymentStatus,
                    b.BookingType,
                    b.IsStudent,
                    b.TotalAmount,
                    b.BalanceAmount,
                    b.ActualEnd,
                    ResourceType = b.Resource.ResourceType,
                    PackageType  = b.ServicePackage.PackageType
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // ── Revenue collected for period bookings ─────────────────────────
            // Scoped to payments belonging to bookings that started in the period,
            // so that "today's revenue" reflects collections for today's bookings
            // rather than all payments timestamped today (which would bleed in
            // payments from older bookings processed on this date).

            var periodBookingIds = periodBookings.Select(b => b.Id).ToList();

            var revenueCollected = periodBookingIds.Count == 0 ? 0m :
                await _dbContext.BookingPayments
                    .Where(p => periodBookingIds.Contains(p.BookingId) &&
                                p.PaidAt != null &&
                                (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                                 p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                    .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            // ── Period stats ──────────────────────────────────────────────────

            var period = new PeriodStatsDto
            {
                PeriodLabel      = periodLabel,
                PeriodStart      = periodStart,
                PeriodEnd        = periodEnd,
                TotalCheckIns    = periodBookings.Count,
                TotalCheckOuts   = periodBookings.Count(b => b.ActualEnd >= periodStart && b.ActualEnd < periodEnd),
                WalkInCount      = periodBookings.Count(b => b.BookingType == BookingType.WalkIn),
                StudentCount     = periodBookings.Count(b => b.IsStudent),
                RevenueCollected = revenueCollected,
                TotalBilled      = periodBookings.Sum(b => b.TotalAmount),
                TotalOutstanding = periodBookings
                    .Where(b => b.PaymentStatus != PaymentStatus.FullyPaid)
                    .Sum(b => b.BalanceAmount),
                UnpaidCount        = periodBookings.Count(b => b.PaymentStatus == PaymentStatus.Unpaid),
                PartiallyPaidCount = periodBookings.Count(b => b.PaymentStatus == PaymentStatus.PartiallyPaid),
                FullyPaidCount     = periodBookings.Count(b => b.PaymentStatus == PaymentStatus.FullyPaid)
            };

            // ── Breakdowns ────────────────────────────────────────────────────

            var byResourceType = periodBookings
                .GroupBy(b => b.ResourceType)
                .Select(g => new ResourceTypeBreakdownDto
                {
                    ResourceType = g.Key,
                    CheckInCount = g.Count(),
                    TotalBilled  = g.Sum(b => b.TotalAmount)
                })
                .OrderBy(x => x.ResourceType)
                .ToList();

            var byPackageType = periodBookings
                .GroupBy(b => b.PackageType)
                .Select(g => new PackageTypeBreakdownDto
                {
                    PackageType  = g.Key,
                    CheckInCount = g.Count(),
                    TotalBilled  = g.Sum(b => b.TotalAmount)
                })
                .OrderBy(x => x.PackageType)
                .ToList();

            // ── Overall (all-time, no period filter) ──────────────────────────

            var totalResources  = await _dbContext.CoworkingResources.CountAsync(cancellationToken);
            var activeResources = await _dbContext.CoworkingResources.CountAsync(r => r.IsActive, cancellationToken);
            var totalPackages   = await _dbContext.ServicePackages.CountAsync(cancellationToken);
            var activePackages  = await _dbContext.ServicePackages.CountAsync(p => p.IsActive, cancellationToken);
            var totalBookings   = await _dbContext.Bookings.CountAsync(cancellationToken);

            var totalRevenue = await _dbContext.BookingPayments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var overall = new OverallStatsDto
            {
                TotalResources  = totalResources,
                ActiveResources = activeResources,
                TotalPackages   = totalPackages,
                ActivePackages  = activePackages,
                TotalBookings   = totalBookings,
                TotalRevenue    = totalRevenue
            };

            // ── Recent bookings (last 5 regardless of period) ─────────────────

            var recentBookings = await _dbContext.Bookings
                .Include(b => b.Resource)
                .Include(b => b.Guest)
                .OrderByDescending(b => b.ScheduledStart)
                .Take(5)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new AdminDashboardResponse
            {
                Live           = live,
                Period         = period,
                ByResourceType = byResourceType,
                ByPackageType  = byPackageType,
                Overall        = overall,
                RecentBookings = recentBookings.Select(b => new BookingSessionResponse
                {
                    BookingId           = b.Id,
                    ReferenceNumber     = b.ReferenceNumber,
                    GuestName           = b.Guest != null
                                            ? $"{b.Guest.FirstName} {b.Guest.LastName}"
                                            : string.Empty,
                    ResourceName        = b.Resource.Name,
                    BookingStatus       = b.BookingStatus,
                    PaymentStatus       = b.PaymentStatus,
                    ScheduledStart      = b.ScheduledStart,
                    ScheduledEnd        = b.ScheduledEnd,
                    ActualStart         = b.ActualStart,
                    SnapshotPackageName = b.SnapshotPackageName,
                    TotalAmount         = b.TotalAmount,
                    BalanceAmount       = b.BalanceAmount
                }).ToList()
            };
        }

        // ── Period resolution ─────────────────────────────────────────────────

        private static (DateTime start, DateTime end, string label) ResolvePeriod(string period)
        {
            var today = DateTime.UtcNow.Date;

            switch (period)
            {
                case "this_week":
                {
                    int dow          = (int)today.DayOfWeek;           // Sun=0, Mon=1 … Sat=6
                    int daysToMonday = dow == 0 ? 6 : dow - 1;
                    var weekStart    = today.AddDays(-daysToMonday);
                    var weekEnd      = weekStart.AddDays(7);

                    var sunday = weekEnd.AddDays(-1);
                    string label = weekStart.Month == sunday.Month
                        ? $"{weekStart:MMM d}–{sunday.Day}, {sunday.Year}"
                        : $"{weekStart:MMM d} – {sunday:MMM d}, {sunday.Year}";

                    return (weekStart, weekEnd, label);
                }

                case "this_month":
                {
                    var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var monthEnd   = monthStart.AddMonths(1);
                    return (monthStart, monthEnd, monthStart.ToString("MMMM yyyy"));
                }

                case "this_year":
                {
                    var yearStart = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var yearEnd   = yearStart.AddYears(1);
                    return (yearStart, yearEnd, today.Year.ToString());
                }

                case "today": // "today"
                {
                    var dayStart = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
                    var dayEnd   = dayStart.AddDays(1);
                    return (dayStart, dayEnd, $"Today, {dayStart:MMMM d}");
                }
                    default:
                {
                    // Fallback to "today" if unrecognized period value
                    var dayStart = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
                    var dayEnd   = dayStart.AddDays(1);
                    return (dayStart, dayEnd, $"Today, {dayStart:MMMM d}");
                }

            }
        }
    }
}
