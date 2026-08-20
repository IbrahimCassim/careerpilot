namespace CareerPilot.Domain;

public enum JobReviewStatus { New, Saved, Dismissed, Applied, Closed }
public enum ApplicationStatus { Draft, Ready, Applied, Screening, Interview, Offer, Rejected, Withdrawn }
public enum ScrapeRunStatus { Running, Succeeded, Partial, Failed, Skipped }
public enum ScrapeRequestStatus { Pending, Running, Completed, Failed }
public enum EvidenceKind { Role, Achievement, Project, Skill, Education, Certification, Portfolio }
public enum WorkMode { Any, OnSite, Hybrid, Remote }

public sealed class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public WorkMode WorkMode { get; set; }
    public string Description { get; set; } = "";
    public string CanonicalUrl { get; set; } = "";
    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public JobReviewStatus ReviewStatus { get; set; }
    public double MatchScore { get; set; }
    public string MatchExplanationJson { get; set; } = "{}";
    public string ContentFingerprint { get; set; } = "";
    public ICollection<SourceListing> SourceListings { get; set; } = [];
}

public sealed class SourceListing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid CollectionSourceId { get; set; }
    public CollectionSource CollectionSource { get; set; } = null!;
    public string ExternalId { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CareerEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public EvidenceKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Organisation { get; set; } = "";
    public string Description { get; set; } = "";
    public string SkillsCsv { get; set; } = "";
    public DateOnly? StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }
    public bool ApprovedForApplications { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SearchPreferences
{
    public int Id { get; set; } = 1;
    public string TargetTitlesCsv { get; set; } = "Software Engineer,Developer";
    public string TitleSynonymsJson { get; set; } = "{}";
    public string LocationsCsv { get; set; } = "Australia";
    public WorkMode WorkMode { get; set; }
    public string PositiveKeywordsCsv { get; set; } = "";
    public string NegativeKeywordsCsv { get; set; } = "";
    public string KnockoutKeywordsCsv { get; set; } = "";
    public int MaxAgeDays { get; set; } = 30;
    public double MinimumScore { get; set; } = 45;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
    public ICollection<DocumentVersion> Documents { get; set; } = [];
}

public sealed class DocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public JobApplication Application { get; set; } = null!;
    public string Kind { get; set; } = "resume";
    public int Version { get; set; }
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string MimeType { get; set; } = "application/octet-stream";
    public string EvidenceIdsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CollectionSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "jsonld";
    public string SearchUrl { get; set; } = "";
    public bool UseBrowser { get; set; }
    public bool Enabled { get; set; } = true;
    public int RequestDelayMs { get; set; } = 1500;
    public int MaximumPages { get; set; } = 2;
    public DateTimeOffset? LastSucceededAt { get; set; }
    public string LastError { get; set; } = "";
}

public sealed class ScrapeRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CollectionSourceId { get; set; }
    public CollectionSource? CollectionSource { get; set; }
    public string RunKey { get; set; } = "";
    public ScrapeRunStatus Status { get; set; } = ScrapeRunStatus.Running;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int DiscoveredCount { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public string Error { get; set; } = "";
}

public sealed class ScrapeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScrapeRequestStatus Status { get; set; } = ScrapeRequestStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string Error { get; set; } = "";
}
