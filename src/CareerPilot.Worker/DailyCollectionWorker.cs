using CareerPilot.Domain;
using CareerPilot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Worker;

public sealed class DailyCollectionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DailyCollectionWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeOnly ScheduledAt = new(2, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForDatabaseAsync(stoppingToken);
        logger.LogInformation("CareerPilot collector is ready; daily schedule is {Time} Australia/Brisbane", ScheduledAt);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessManualRequestsAsync(stoppingToken);
                await ProcessScheduledRunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Collection scheduler iteration failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task WaitForDatabaseAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CareerPilotDbContext>();
                if (await db.Database.CanConnectAsync(cancellationToken)) return;
            }
            catch (Exception exception) { logger.LogWarning(exception, "Waiting for API to migrate the database"); }
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private async Task ProcessManualRequestsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareerPilotDbContext>();
        var request = await db.ScrapeRequests.OrderBy(x => x.RequestedAt)
            .FirstOrDefaultAsync(x => x.Status == ScrapeRequestStatus.Pending, cancellationToken);
        if (request is null) return;
        request.Status = ScrapeRequestStatus.Running;
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await scope.ServiceProvider.GetRequiredService<ScrapeCoordinator>()
                .RunAllAsync($"manual:{request.Id:N}", cancellationToken);
            request.Status = ScrapeRequestStatus.Completed;
        }
        catch (Exception exception)
        {
            request.Status = ScrapeRequestStatus.Failed;
            request.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
        }
        request.CompletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessScheduledRunAsync(CancellationToken cancellationToken)
    {
        var timezone = BrisbaneTimeZone();
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timezone);
        if (TimeOnly.FromDateTime(localNow.DateTime) < ScheduledAt) return;
        await using var scope = scopeFactory.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ScrapeCoordinator>();
        await coordinator.RunAllAsync($"scheduled:{DateOnly.FromDateTime(localNow.DateTime):yyyy-MM-dd}", cancellationToken);
    }

    private static TimeZoneInfo BrisbaneTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time"); }
    }
}
