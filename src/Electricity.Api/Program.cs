using Electricity.Application.Interfaces;
using Electricity.Application.Services;
using Electricity.Infrastructure;
using Electricity.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IElectricityDataService, ElectricityDataService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ElectricityDbContext>("database");

// Problem Details (RFC 7807)
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Electricity Aggregation API",
        Version = "v1",
        Description = "API for querying aggregated electricity consumption data from Lithuanian apartments",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "API Support",
            Email = "support@example.com"
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Global Exception Handler
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandlerFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred");

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request",
            Detail = app.Environment.IsDevelopment() ? exception?.Message : "Please try again later",
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Electricity Aggregation API v1");
        c.DisplayRequestDuration();
    });
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthorization();

// Health Check Endpoint
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
