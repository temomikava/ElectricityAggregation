using Electricity.Application.Interfaces;
using Electricity.Infrastructure.Configuration;
using Electricity.Infrastructure.Data;
using Electricity.Infrastructure.ExternalServices;
using Electricity.Infrastructure.Repositories;
using Electricity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Electricity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ElectricityDbContext>(options =>
            options.UseNpgsql(connStr,
                b => b.MigrationsAssembly(typeof(ElectricityDbContext).Assembly.FullName)));

        services.AddScoped<IConsumptionRepository, ConsumptionRepository>();
        services.AddScoped<IProcessingLogRepository, ProcessingLogRepository>();

        services.Configure<DownloadOptions>(configuration.GetSection(DownloadOptions.SectionName));

        services.AddHttpClient<IDataSourceRepository, DataGovLtDataSourceRepository>();

        services.AddScoped<ICsvParser, CsvParserService>();

        return services;
    }
}
