using Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class ToggleServicePackageCommand : IRequest<object>
    {
        public Guid Id { get; }
        public ToggleServicePackageCommand(Guid id) => Id = id;
    }

    public class ToggleServicePackageCommandHandler
        : IRequestHandler<ToggleServicePackageCommand, object>
    {
        private readonly ApplicationDbContext _dbContext;

        public ToggleServicePackageCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<object> Handle(
            ToggleServicePackageCommand command, CancellationToken cancellationToken)
        {
            var package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken)
                ?? throw new NotFoundException($"Service package with ID {command.Id} was not found.");

            package.IsActive = !package.IsActive;
            package.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new
            {
                id = package.Id,
                name = package.Name,
                isActive = package.IsActive,
                message = package.IsActive
                    ? $"'{package.Name}' has been activated."
                    : $"'{package.Name}' has been deactivated."
            };
        }
    }
}
