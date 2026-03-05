using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using Domain.Entity.Coworking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class CreateServicePackageCommand : IRequest<ServicePackageResponse>
    {
        public CreateServicePackageRequest Request { get; }
        public CreateServicePackageCommand(CreateServicePackageRequest request) => Request = request;
    }

    public class CreateServicePackageCommandHandler
        : IRequestHandler<CreateServicePackageCommand, ServicePackageResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public CreateServicePackageCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ServicePackageResponse> Handle(
            CreateServicePackageCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            var nameExists = await _dbContext.ServicePackages
                .AnyAsync(p =>
                    p.Name.ToLower() == req.Name.ToLower() &&
                    p.ResourceType == req.ResourceType,
                    cancellationToken);

            if (nameExists)
                throw new ConflictException(
                    $"A package named '{req.Name}' already exists for {req.ResourceType}.");

            var package = new ServicePackage
            {
                Id = Guid.NewGuid(),
                Name = req.Name,
                ResourceType = req.ResourceType,
                PackageType = req.PackageType,
                PriceAmount = req.PriceAmount,
                DurationHours = req.DurationHours,
                DurationDays = req.DurationDays,
                MaxCapacity = req.MaxCapacity,
                IsStudentEligible = req.IsStudentEligible,
                StudentDiscountPercent = req.StudentDiscountPercent,
                DownpaymentPercent = req.DownpaymentPercent,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ServicePackages.Add(package);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return MapToResponse(package);
        }

        internal static ServicePackageResponse MapToResponse(ServicePackage p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            ResourceType = p.ResourceType,
            PackageType = p.PackageType,
            PriceAmount = p.PriceAmount,
            DurationHours = p.DurationHours,
            DurationDays = p.DurationDays,
            MaxCapacity = p.MaxCapacity,
            IsStudentEligible = p.IsStudentEligible,
            StudentDiscountPercent = p.StudentDiscountPercent,
            DownpaymentPercent = p.DownpaymentPercent,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
