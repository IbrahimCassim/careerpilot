using CareerPilot.Domain;
using CareerPilot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Worker;

public sealed class ScrapeCoordinator(
    CareerPilotDbContext db,
    IEnumerable<IJobSourceAdapter> adapters,
    JobIngestionService ingestion,
    ILogger<ScrapeCoordinator> logger,
    TimeProvider timeProvider)
{
    public async Task RunAllAsync(string parentRunKey, CancellationToken cancellationToken)
    {
        var sources = await db.CollectionSources.Where(x => x.Enabled && x.Kind != "manual").OrderBy(x => x.Name).ToListAsync(cancellationToken);
        foreach (var source in sources)
        {
            var runKey = $"{parentRunKey}:{source.Id:N}";
            if (await db.ScrapeRuns.AnyAsync(x => x.RunKey == runKey, cancellationToken)) continue;
            var run = new ScrapeRun { CollectionSourceId = source.Id, RunKey = runKey, StartedAt = timeProvider.GetUtcNow() };
            db.ScrapeRuns.Add(run);
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                var adapter = adapters.SingleOrDefault(x => x.Kind == source.Kind)
                    ?? throw new InvalidOperationException($"No adapter is registered for source kind '{source.Kind}'.");
                logger.LogInformation("Collecting {Source} with {Adapter}", source.Name, adapter.Kind);
                var jobs = await adapter.DiscoverAsync(source, cancellationToken);
                var result = await ingestion.IngestAsync(source, jobs, cancellationToken);
                run.DiscoveredCount = jobs.Count; run.AddedCount = result.Added; run.UpdatedCount = result.Updated;
                run.Status = ScrapeRunStatus.Succeeded; source.LastSucceededAt = timeProvider.GetUtcNow(); source.LastError = "";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                run.Status = ScrapeRunStatus.Failed;
                run.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                source.LastError = run.Error;
                logger.LogWarning(exception, "Source {Source} failed; continuing with remaining sources", source.Name);
            }
            finally
            {
                run.CompletedAt = timeProvider.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
                if (source.RequestDelayMs > 0) await Task.Delay(source.RequestDelayMs, cancellationToken);
            }
        }
    }
}
