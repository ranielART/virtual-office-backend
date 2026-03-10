using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetAdminDashboardQuery : IRequest<AdminDashboardResponse> { }

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
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd   = todayStart.AddDays(1);

            // ── Today ────────────────────────────────────────────────────────────

            var todayBookings = await _dbContext.Bookings
                .Where(b => b.ScheduledStart >= todayStart && b.ScheduledStart < todayEnd)
                .ToListAsync(cancellationToken);

            var todayRevenue = await _dbContext.BookingPayments
                .Where(p => p.PaidAt >= todayStart && p.PaidAt < todayEnd &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .SumAsync(p => p.Amount, cancellationToken);

            var pendingValidations = await _dbContext.BookingPayments
                .CountAsync(p => p.PaymentValidationStatus == PaymentValidationStatus.PendingValidation,
                            cancellationToken);

            // ── Overall ───────────────────────────────────────────────────────────

            var totalResources  = await _dbContext.CoworkingResources.CountAsync(cancellationToken);
            var activeResources = await _dbContext.CoworkingResources.CountAsync(r => r.IsActive, cancellationToken);
            var totalPackages   = await _dbContext.ServicePackages.CountAsync(cancellationToken);
            var activePackages  = await _dbContext.ServicePackages.CountAsync(p => p.IsActive, cancellationToken);
            var totalBookings   = await _dbContext.Bookings.CountAsync(cancellationToken);

            var totalRevenue = await _dbContext.BookingPayments
                .Where(p => p.PaidAt != null &&
                            (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                             p.PaymentValidationStatus == PaymentValidationStatus.Validated))
                .SumAsync(p => p.Amount, cancellationToken);

            // ── Recent bookings (last 5) ──────────────────────────────────────────

            var recentBookings = await _dbContext.Bookings
                .Include(b => b.Resource)
                .Include(b => b.Guest)
                .OrderByDescending(b => b.ScheduledStart)
                .Take(5)
                .ToListAsync(cancellationToken);

            return new AdminDashboardResponse
            {
                Today = new TodayStatsDto
                {
                    TotalBookings             = todayBookings.Count,
                    ActiveSessions            = todayBookings.Count(b => b.BookingStatus == BookingStatus.CheckedIn),
                    OverstayedSessions        = todayBookings.Count(b => b.BookingStatus == BookingStatus.Overstayed),
                    PendingPaymentValidations = pendingValidations,
                    Revenue                   = todayRevenue
                },
                Overall = new OverallStatsDto
                {
                    TotalResources  = totalResources,
                    ActiveResources = activeResources,
                    TotalPackages   = totalPackages,
                    ActivePackages  = activePackages,
                    TotalBookings   = totalBookings,
                    TotalRevenue    = totalRevenue
                },
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
    }
}
