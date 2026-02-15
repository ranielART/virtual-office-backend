using Infrastructure.Services;
using Application.services.User;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            // Register Token Service
            services.AddScoped<ITokenService, TokenService>();

            return services;
        }
    }
}
