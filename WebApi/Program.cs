using Application;
using DependencyInjection;
using DotNetEnv;
using Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence;
using Persistence.Data;
using Persistence.Seeders;
using Serilog;

// Load environment variables from .env file at repo root
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
if (!File.Exists(envPath))
{
    // Fallback: try current directory (for when running from repo root)
    envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
}

if (File.Exists(envPath))
{
    Env.Load(envPath);
}
else
{
    Console.WriteLine($"WARNING: .env file not found. Checked paths:");
    Console.WriteLine($"  - {Path.Combine(Directory.GetCurrentDirectory(), "..", ".env")}");
    Console.WriteLine($"  - {Path.Combine(Directory.GetCurrentDirectory(), ".env")}");
}

var builder = WebApplication.CreateBuilder(args);
{
    // Build connection string from environment variables
    var connectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                           $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                           $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                           $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                           $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};";
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

    // Build JWT configuration from environment variables
    builder.Configuration["Jwt:Secret"] = Environment.GetEnvironmentVariable("JWT_SECRET");
    builder.Configuration["Jwt:ExpiryMinutes"] = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES");
    builder.Configuration["Jwt:RefreshTokenExpiryDays"] = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRY_DAYS");

    builder.AddSerilogLogging();
    // Add Persistence first (registers DbContext)
    builder.Services.AddPersistence(builder.Configuration);

    // Add Identity BEFORE Application (registers UserManager for MediatR handlers)
    builder.Services.AddIdentity<Domain.Entity.Identity.User, Domain.Entity.Identity.Role>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // Add services of the layers to the container
    builder.Services
        .AddWebApi(builder.Configuration)
        .AddApplication()
        .AddInfrastructure()
        .AddHealthChecks();

}


var app = builder.Build();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        // 1. Apply Schema Changes (Create Tables)
        dbContext.Database.Migrate();

        // 2. Run Data Seeder (Insert Admin User) -> THIS IS THE NEW PART
        var seeder = services.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        // Good practice to log this, otherwise the app might crash silently on startup
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.UseExceptionHandler();


app.UseStatusCodePages();
app.UseCors("Cors");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.EnablePersistAuthorization();
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();