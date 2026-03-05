using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class UpdateServicePackageCommand : IRequest<ServicePackageResponse>
    {
        public Guid Id { get; }
        public UpdateServicePackageRequest Request { get; }

        public UpdateServicePackageCommand(Guid id, UpdateServicePackageRequest request)
        {
            Id = id;
            Request = request;
        }
    }

    public class UpdateServicePackageCommandHandler
        : IRequestHandler<UpdateServicePackageCommand, ServicePackageResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public UpdateServicePackageCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ServicePackageResponse> Handle(
            UpdateServicePackageCommand command, CancellationToken cancellationToken)
        {
            var package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken)
                ?? throw new NotFoundException($"Service package with ID {command.Id} was not found.");

            var req = command.Request;

            if (req.Name is not null)
            {
                var resourceType = req.ResourceType ?? package.ResourceType;
                var nameExists = await _dbContext.ServicePackages
                    .AnyAsync(p =>
                        p.Name.ToLower() == req.Name.ToLower() &&
                        p.ResourceType == resourceType &&
                        p.Id != command.Id,
                        cancellationToken);

                if (nameExists)
                    throw new ConflictException(
                        $"A package named '{req.Name}' already exists for {resourceType}.");

                package.Name = req.Name;
            }

            if (req.ResourceType.HasValue) package.ResourceType = req.ResourceType.Value;
            if (req.PackageType.HasValue) package.PackageType = req.PackageType.Value;
            if (req.PriceAmount.HasValue) package.PriceAmount = req.PriceAmount.Value;
            if (req.DurationHours.HasValue) package.DurationHours = req.DurationHours.Value;
            if (req.DurationDays.HasValue) package.DurationDays = req.DurationDays.Value;
            if (req.MaxCapacity.HasValue) package.MaxCapacity = req.MaxCapacity.Value;
            if (req.IsStudentEligible.HasValue) package.IsStudentEligible = req.IsStudentEligible.Value;
            if (req.StudentDiscountPercent.HasValue) package.StudentDiscountPercent = req.StudentDiscountPercent.Value;
            if (req.DownpaymentPercent.HasValue) package.DownpaymentPercent = req.DownpaymentPercent.Value;

            package.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreateServicePackageCommandHandler.MapToResponse(package);
        }
    }
}
