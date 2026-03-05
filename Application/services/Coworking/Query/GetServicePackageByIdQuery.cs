using Application.Common.Exceptions;
using Application.Services.Coworking.Command;
using Application.Services.Coworking.Response;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetServicePackageByIdQuery : IRequest<ServicePackageResponse>
    {
        public Guid Id { get; }
        public GetServicePackageByIdQuery(Guid id) => Id = id;
    }

    public class GetServicePackageByIdQueryHandler
        : IRequestHandler<GetServicePackageByIdQuery, ServicePackageResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetServicePackageByIdQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ServicePackageResponse> Handle(
            GetServicePackageByIdQuery request, CancellationToken cancellationToken)
        {
            var package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException($"Service package with ID {request.Id} was not found.");

            return CreateServicePackageCommandHandler.MapToResponse(package);
        }
    }
}
