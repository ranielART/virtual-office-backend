using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using Domain.Entity.Coworking;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class ProcessWalkInTimeInCommand : IRequest<WalkInTimeInResponse>
    {
        public WalkInTimeInRequest Request { get; }
        public string FrontDeskUserId { get; }

        public ProcessWalkInTimeInCommand(WalkInTimeInRequest request, string frontDeskUserId)
        {
            Request = request;
            FrontDeskUserId = frontDeskUserId;
        }
    }

    public class ProcessWalkInTimeInCommandHandler
        : IRequestHandler<ProcessWalkInTimeInCommand, WalkInTimeInResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public ProcessWalkInTimeInCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<WalkInTimeInResponse> Handle(
            ProcessWalkInTimeInCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            // 1. Validate resource
            var resource = await _dbContext.CoworkingResources
                .FirstOrDefaultAsync(r => r.Id == req.ResourceId && r.IsActive, cancellationToken)
                ?? throw new NotFoundException($"Resource '{req.ResourceId}' not found or is inactive.");

            // 2. Validate package
            var package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == req.ServicePackageId && p.IsActive, cancellationToken)
                ?? throw new NotFoundException($"Service package '{req.ServicePackageId}' not found or is inactive.");

            // 3. Begin transaction
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var requestedStart = now;
                var requestedEnd   = now.AddHours((double)req.EstimatedHours);

                // 4. Re-check availability inside transaction
                var isUnavailable = await _dbContext.Bookings.AnyAsync(b =>
                    b.ResourceId == req.ResourceId &&
                    b.BookingStatus != BookingStatus.Cancelled &&
                    b.BookingStatus != BookingStatus.NoShow &&
                    b.BookingStatus != BookingStatus.CheckedOut &&
                    b.BookingStatus != BookingStatus.Overstayed &&
                    b.ScheduledStart < requestedEnd &&
                    b.ScheduledEnd   > requestedStart,
                    cancellationToken);

                if (isUnavailable)
                    throw new ConflictException("This resource is currently occupied.");

                // 5. Generate reference number using PostgreSQL sequence
                var nextVal = await _dbContext.Database
                    .SqlQuery<long>($"SELECT nextval('booking_ref_seq')")
                    .FirstAsync(cancellationToken);
                var referenceNumber = $"BKG-{nextVal:D4}";

                // 6. Compute SnapshotHourlyRate
                decimal snapshotHourlyRate;
                if (package.PackageType == PackageType.Hourly)
                {
                    snapshotHourlyRate = package.PriceAmount;
                }
                else if (package.DurationHours.HasValue)
                {
                    snapshotHourlyRate = package.PriceAmount / package.DurationHours.Value;
                }
                else if (package.DurationDays.HasValue)
                {
                    snapshotHourlyRate = package.PriceAmount / (package.DurationDays.Value * 8m);
                }
                else
                {
                    // Meeting room flat package — look up the active Hourly package for same ResourceType
                    var hourlyPackage = await _dbContext.ServicePackages
                        .FirstOrDefaultAsync(p =>
                            p.ResourceType == resource.ResourceType &&
                            p.PackageType   == PackageType.Hourly &&
                            p.IsActive,
                            cancellationToken);
                    snapshotHourlyRate = hourlyPackage?.PriceAmount ?? 0m;
                }

                // 7. Compute pricing
                var baseAmount = package.PackageType == PackageType.Hourly
                    ? req.EstimatedHours * package.PriceAmount
                    : package.PriceAmount;

                var discountAmount = req.IsStudent && package.IsStudentEligible
                    ? baseAmount * (package.StudentDiscountPercent / 100m)
                    : 0m;

                var totalAmount   = baseAmount - discountAmount;
                var balanceAmount = totalAmount;

                // 8. Create BookingGuest
                var guest = new BookingGuest
                {
                    Id                = Guid.NewGuid(),
                    FirstName         = req.GuestFirstName,
                    LastName          = req.GuestLastName,
                    Phone             = req.GuestPhone,
                    Email             = req.GuestEmail,
                    IsStudentVerified = req.IsStudent,
                    CreatedAt         = now,
                    UpdatedAt         = now
                };

                // 9. Create Booking
                var booking = new Booking
                {
                    Id                   = Guid.NewGuid(),
                    ReferenceNumber      = referenceNumber,
                    BookingType          = BookingType.WalkIn,
                    BookingStatus        = BookingStatus.CheckedIn,
                    PaymentStatus        = PaymentStatus.Unpaid,
                    ResourceId           = req.ResourceId,
                    ServicePackageId     = req.ServicePackageId,
                    FrontDeskUserId      = command.FrontDeskUserId,
                    ScheduledStart       = requestedStart,
                    ScheduledEnd         = requestedEnd,
                    ActualStart          = requestedStart,
                    IsStudent            = req.IsStudent,
                    PaxCount             = 1,
                    DownpaymentAmount    = 0m,
                    SnapshotPackageName  = package.Name,
                    SnapshotPricePerUnit = package.PriceAmount,
                    SnapshotHourlyRate   = snapshotHourlyRate,
                    BaseAmount           = baseAmount,
                    DiscountAmount       = discountAmount,
                    TotalAmount          = totalAmount,
                    BalanceAmount        = balanceAmount,
                    Guest                = guest,
                    CreatedAt            = now,
                    UpdatedAt            = now
                };

                // 10. Persist and commit
                _dbContext.Bookings.Add(booking);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new WalkInTimeInResponse
                {
                    BookingId           = booking.Id,
                    ReferenceNumber     = booking.ReferenceNumber,
                    GuestName           = $"{guest.FirstName} {guest.LastName}",
                    ResourceName        = resource.Name,
                    ScheduledStart      = booking.ScheduledStart,
                    ScheduledEnd        = booking.ScheduledEnd,
                    SnapshotPackageName = booking.SnapshotPackageName,
                    IsStudent           = booking.IsStudent,
                    BaseAmount          = booking.BaseAmount,
                    DiscountAmount      = booking.DiscountAmount,
                    TotalAmount         = booking.TotalAmount
                };
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
