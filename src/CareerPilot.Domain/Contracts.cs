namespace CareerPilot.Domain;

public sealed record DiscoveredJob(
    string ExternalId,
    string Title,
    string Company,
    string Location,
    string Description,
    string Url,
    DateTimeOffset? PostedAt,
    WorkMode WorkMode = WorkMode.Any);

public sealed record MatchFactor(string Name, double Points, string Explanation);
public sealed record MatchExplanation(
    double Score,
    bool IsKnockedOut,
    IReadOnlyList<MatchFactor> Factors,
    IReadOnlyList<string> MissingRequirements);

public interface IJobSourceAdapter
{
    string Kind { get; }
    Task<IReadOnlyList<DiscoveredJob>> DiscoverAsync(CollectionSource source, CancellationToken cancellationToken);
}

public interface IApplicationHandoff
{
    string Mode { get; }
    Task<ApplicationHandoffResult> PrepareAsync(JobApplication application, CancellationToken cancellationToken);
}

public sealed record ApplicationHandoffResult(string ApplicationUrl, IReadOnlyList<string> DownloadUrls);
