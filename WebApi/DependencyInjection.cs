using Microsoft.Extensions.DependencyInjection;

namespace WebApi
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddWebApi(this IServiceCollection services)
        {
            // Web API services registration goes here

            services.AddControllers();
            services.AddHealthChecks();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            //services.AddOpenApi();

            return services;
        }
    }
}