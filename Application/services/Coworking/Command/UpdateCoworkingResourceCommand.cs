using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class UpdateCoworkingResourceCommand : IRequest<CoworkingResourceResponse>
    {
        public Guid Id { get; }
        public UpdateCoworkingResourceRequest Request { get; }

        public UpdateCoworkingResourceCommand(Guid id, UpdateCoworkingResourceRequest request)
        {
            Id = id;
            Request = request;
        }
    }

    public class UpdateCoworkingResourceCommandHandler
        : IRequestHandler<UpdateCoworkingResourceCommand, CoworkingResourceResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public UpdateCoworkingResourceCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CoworkingResourceResponse> Handle(
            UpdateCoworkingResourceCommand command, CancellationToken cancellationToken)
        {
            var resource = await _dbContext.CoworkingResources
                .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
                ?? throw new NotFoundException($"Resource with ID {command.Id} was not found.");

            var req = command.Request;

            if (req.Name is not null)
            {
                var nameExists = await _dbContext.CoworkingResources
                    .AnyAsync(r => r.Name.ToLower() == req.Name.ToLower() && r.Id != command.Id,
                        cancellationToken);

                if (nameExists)
                    throw new ConflictException($"A resource named '{req.Name}' already exists.");

                resource.Name = req.Name;
            }

            if (req.Capacity.HasValue) resource.Capacity = req.Capacity.Value;
            if (req.Description is not null) resource.Description = req.Description;
            if (req.SortOrder.HasValue) resource.SortOrder = req.SortOrder.Value;

            resource.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new CoworkingResourceResponse
            {
                Id = resource.Id,
                Name = resource.Name,
                ResourceType = resource.ResourceType,
                Capacity = resource.Capacity,
                Description = resource.Description,
                IsActive = resource.IsActive,
                SortOrder = resource.SortOrder,
                CreatedAt = resource.CreatedAt,
                UpdatedAt = resource.UpdatedAt
            };
        }
    }
}
