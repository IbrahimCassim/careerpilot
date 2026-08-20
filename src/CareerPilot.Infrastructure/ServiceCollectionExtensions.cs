using CareerPilot.Application;
using CareerPilot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareerPilot.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCareerPilotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CareerPilot") ?? "Data Source=data/careerpilot.db;Cache=Shared;Default Timeout=30;Pooling=True";
        services.AddDbContextFactory<CareerPilotDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<CareerPilotDbContext>>().CreateDbContext());
        services.AddSingleton<ScoringService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<JobIngestionService>();
        services.AddSingleton<JsonLdParser>();
        services.AddSingleton<IJobSourceAdapter, JsonLdHttpAdapter>();
        services.AddSingleton<IJobSourceAdapter, JsonLdBrowserAdapter>();
        services.AddHttpClient<JsonLdHttpAdapter>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CareerPilot/1.0 personal-job-search (+https://github.com/)");
        }).AddStandardResilienceHandler();
        return services;
    }

    public static async Task ConfigureSqliteAsync(this CareerPilotDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
    }
}
