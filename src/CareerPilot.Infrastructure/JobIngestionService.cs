using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerPilot.Application;
using CareerPilot.Domain;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure;

public sealed class JobIngestionService(CareerPilotDbContext db, ScoringService scoring)
{
    public async Task<(int Added, int Updated)> IngestAsync(CollectionSource source, IEnumerable<DiscoveredJob> discovered, CancellationToken cancellationToken)
    {
        var preferences = await db.SearchPreferences.SingleAsync(cancellationToken);
        var evidence = await db.CareerEvidence.AsNoTracking().Where(x => x.ApprovedForApplications).ToListAsync(cancellationToken);
        var added = 0;
        var updated = 0;
        foreach (var item in discovered)
        {
            var canonical = Canonicalize(item.Url);
            var job = await db.Jobs.Include(x => x.SourceListings).SingleOrDefaultAsync(x => x.CanonicalUrl == canonical, cancellationToken);
            if (job is null)
            {
                job = new Job { CanonicalUrl = canonical, FirstSeenAt = DateTimeOffset.UtcNow };
                db.Jobs.Add(job);
                added++;
            }
            else updated++;
            job.Title = item.Title.Trim();
            job.Company = item.Company.Trim();
            job.Location = item.Location.Trim();
            job.Description = item.Description.Trim();
            job.PostedAt = item.PostedAt;
            job.WorkMode = item.WorkMode;
            job.LastSeenAt = DateTimeOffset.UtcNow;
            job.ContentFingerprint = Fingerprint(item);
            var explanation = scoring.Score(job, preferences, evidence);
            job.MatchScore = explanation.Score;
            job.MatchExplanationJson = ScoringService.Serialize(explanation);

            var listing = job.SourceListings.SingleOrDefault(x => x.CollectionSourceId == source.Id && x.ExternalId == item.ExternalId);
            if (listing is null)
            {
                job.SourceListings.Add(new SourceListing
                {
                    CollectionSourceId = source.Id,
                    ExternalId = item.ExternalId,
                    SourceUrl = item.Url
                });
            }
            else listing.LastSeenAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return (added, updated);
    }

    private static string Canonicalize(string raw)
    {
        var builder = new UriBuilder(raw) { Fragment = "" };
        var kept = builder.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !x.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && !x.StartsWith("ref=", StringComparison.OrdinalIgnoreCase));
        builder.Query = string.Join('&', kept);
        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string Fingerprint(DiscoveredJob job)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(job))));
}
