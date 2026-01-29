
using Application;
using Infrastructure;
using Persistence;
using WebApi;


var builder = WebApplication.CreateBuilder(args);
{
    // Add services of the layers to the container.
    //var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    builder.Services.
        AddWebApi().
        AddApplication().
        AddInfrastructure().AddPersistence(builder.Configuration);

}


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

//app.MapControllers();

app.MapHealthChecks("/health");

app.Run();