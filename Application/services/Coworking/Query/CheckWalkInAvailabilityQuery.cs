using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class CheckWalkInAvailabilityQuery : IRequest<CheckWalkInAvailabilityResponse>
    {
        public Guid ResourceId { get; set; }
        public decimal EstimatedHours { get; set; }
    }

    public class CheckWalkInAvailabilityQueryHandler
        : IRequestHandler<CheckWalkInAvailabilityQuery, CheckWalkInAvailabilityResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public CheckWalkInAvailabilityQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CheckWalkInAvailabilityResponse> Handle(
            CheckWalkInAvailabilityQuery request, CancellationToken cancellationToken)
        {
            var resource = await _dbContext.CoworkingResources
                .FirstOrDefaultAsync(r => r.Id == request.ResourceId && r.IsActive, cancellationToken)
                ?? throw new Application.Common.Exceptions.NotFoundException(
                    $"Resource '{request.ResourceId}' not found or is inactive.");

            var requestedStart = DateTime.UtcNow;
            var requestedEnd   = DateTime.UtcNow.AddHours((double)request.EstimatedHours);

            var isUnavailable = await _dbContext.Bookings.AnyAsync(b =>
                b.ResourceId == request.ResourceId &&
                b.BookingStatus != BookingStatus.Cancelled &&
                b.BookingStatus != BookingStatus.NoShow &&
                b.BookingStatus != BookingStatus.CheckedOut &&
                b.BookingStatus != BookingStatus.Overstayed &&
                b.ScheduledStart < requestedEnd &&
                b.ScheduledEnd   > requestedStart,
                cancellationToken);

            DateTime? nextAvailableAt = null;
            if (isUnavailable)
            {
                nextAvailableAt = await _dbContext.Bookings
                    .Where(b =>
                        b.ResourceId == request.ResourceId &&
                        b.BookingStatus != BookingStatus.Cancelled &&
                        b.BookingStatus != BookingStatus.NoShow &&
                        b.BookingStatus != BookingStatus.CheckedOut &&
                        b.BookingStatus != BookingStatus.Overstayed)
                    .OrderBy(b => b.ScheduledEnd)
                    .Select(b => (DateTime?)b.ScheduledEnd)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return new CheckWalkInAvailabilityResponse
            {
                ResourceId      = resource.Id,
                ResourceName    = resource.Name,
                IsAvailable     = !isUnavailable,
                NextAvailableAt = nextAvailableAt
            };
        }
    }
}
