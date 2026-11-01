using Application.Common.Exceptions;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class ToggleCoworkingResourceCommand : IRequest<object>
    {
        public Guid Id { get; }
        public ToggleCoworkingResourceCommand(Guid id) => Id = id;
    }

    public class ToggleCoworkingResourceCommandHandler
        : IRequestHandler<ToggleCoworkingResourceCommand, object>
    {
        private readonly ApplicationDbContext _dbContext;

        public ToggleCoworkingResourceCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<object> Handle(
            ToggleCoworkingResourceCommand command, CancellationToken cancellationToken)
        {
            var resource = await _dbContext.CoworkingResources
                .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
                ?? throw new NotFoundException($"Resource with ID {command.Id} was not found.");

            // Prevent disabling a resource that has an active checked-in session
            if (resource.IsActive)
            {
                var hasActiveSession = await _dbContext.Bookings
                    .AnyAsync(b =>
                        b.ResourceId == command.Id &&
                        b.BookingStatus == BookingStatus.CheckedIn,
                        cancellationToken);

                if (hasActiveSession)
                    throw new ConflictException(
                        "Cannot disable this resource while a guest is currently checked in. " +
                        "Please process the active session first.");
            }

            resource.IsActive = !resource.IsActive;
            resource.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new
            {
                id = resource.Id,
                name = resource.Name,
                isActive = resource.IsActive,
                message = resource.IsActive
                    ? $"'{resource.Name}' has been enabled."
                    : $"'{resource.Name}' has been disabled."
            };
        }
    }
}
