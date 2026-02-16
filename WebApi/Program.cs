using Application;
using DependencyInjection;
using DotNetEnv;
using Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using Persistence.Seeders;
using Persistence;
// Load environment variables from .env file
var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", ".env");
Env.Load(envPath);

var builder = WebApplication.CreateBuilder(args);
{
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
if (app.Environment.IsDevelopment())
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