using CareerPilot.Infrastructure;
using CareerPilot.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Services.AddCareerPilotInfrastructure(builder.Configuration);
builder.Services.AddScoped<ScrapeCoordinator>();
builder.Services.AddHostedService<DailyCollectionWorker>();

await builder.Build().RunAsync();
