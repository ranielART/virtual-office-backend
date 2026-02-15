using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Application.services.User;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Add MediatR
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

            // Add AutoMapper
            services.AddAutoMapper(typeof(UserMappingProfile));

            return services;
        }
    }
}
