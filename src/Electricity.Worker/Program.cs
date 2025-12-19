using Electricity.Application.Interfaces;
using Electricity.Application.Services;
using Electricity.Infrastructure;
using Electricity.Infrastructure.Data;
using Electricity.Worker;
using Electricity.Worker.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IElectricityDataService, ElectricityDataService>();

builder.Services.Configure<WorkerConfiguration>(builder.Configuration.GetSection("Worker"));

builder.Services.AddHostedService<DataProcessingWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ElectricityDbContext>();
        Log.Information("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error applying database migrations");
        throw;
    }
}

await host.RunAsync();
