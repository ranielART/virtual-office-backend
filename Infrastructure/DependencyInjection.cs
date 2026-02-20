using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Application.Common.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register Storage Configuration from environment variables
            var storageConfig = new StorageConfiguration
            {
                Provider = Environment.GetEnvironmentVariable("AZURE_STORAGE_PROVIDER") ?? "AzureBlob",
                ConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
                    ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING environment variable is not configured"),
                ContainerName = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME") ?? "files",
                AccountName = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT_NAME") ?? "",
                AccountKey = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT_KEY") ?? ""
            };
            services.AddSingleton(storageConfig);

            // Register Storage Service
            services.AddScoped<IStorageService, AzureBlobStorageService>();

            // Register Token Service
            services.AddScoped<ITokenService, TokenService>();

            // Register Email Service
            services.AddScoped<IEmailService, EmailService>();

            return services;
        }
    }
}

