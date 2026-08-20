using System.Security.Claims;
using System.Text.Json;
using CareerPilot.Application;
using CareerPilot.Domain;
using CareerPilot.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Api;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapCareerPilotApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/me", (ClaimsPrincipal user) => new { authenticated = user.Identity?.IsAuthenticated == true, name = user.Identity?.Name }).RequireAuthorization();
        endpoints.MapGet("/api/auth/login", (string? returnUrl) => Results.Challenge(new AuthenticationProperties
            { RedirectUri = SafeReturnUrl(returnUrl) }, ["GitHub"]));
        endpoints.MapPost("/api/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        var api = endpoints.MapGroup("/api").RequireAuthorization();
        api.MapGet("/dashboard", DashboardAsync);
        api.MapGet("/jobs", ListJobsAsync);
        api.MapGet("/jobs/{id:guid}", GetJobAsync);
        api.MapPost("/jobs/import", ImportJobAsync);
        api.MapPatch("/jobs/{id:guid}/status", SetJobStatusAsync);
        api.MapGet("/evidence", async (CareerPilotDbContext db, CancellationToken ct) =>
            await db.CareerEvidence.AsNoTracking().OrderByDescending(x => x.UpdatedAt).ToListAsync(ct));
        api.MapPost("/evidence", SaveEvidenceAsync);
        api.MapDelete("/evidence/{id:guid}", DeleteEvidenceAsync);
        api.MapGet("/preferences", async (CareerPilotDbContext db, CancellationToken ct) => await db.SearchPreferences.AsNoTracking().SingleAsync(ct));
        api.MapPut("/preferences", SavePreferencesAsync);
        api.MapGet("/applications", ListApplicationsAsync);
        api.MapPost("/applications", CreateApplicationAsync);
        api.MapPatch("/applications/{id:guid}", UpdateApplicationAsync);
        api.MapPost("/applications/{id:guid}/documents", CreateDocumentsAsync);
        api.MapGet("/documents/{id:guid}/download", DownloadDocumentAsync);
        api.MapGet("/sources", async (CareerPilotDbContext db, CancellationToken ct) =>
            await db.CollectionSources.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
        api.MapPost("/sources", SaveSourceAsync);
        api.MapDelete("/sources/{id:guid}", DeleteSourceAsync);
        api.MapGet("/scrapes", async (CareerPilotDbContext db, CancellationToken ct) =>
            await db.ScrapeRuns.AsNoTracking().Include(x => x.CollectionSource).OrderByDescending(x => x.StartedAt).Take(50).ToListAsync(ct));
        api.MapPost("/scrapes/run", QueueScrapeAsync);
        return endpoints;
    }

    private static async Task<IResult> DashboardAsync(CareerPilotDbContext db, CancellationToken ct)
    {
        var newJobs = await db.Jobs.CountAsync(x => x.ReviewStatus == JobReviewStatus.New, ct);
        var strongMatches = await db.Jobs.CountAsync(x => x.MatchScore >= 65 && x.ReviewStatus != JobReviewStatus.Dismissed, ct);
        var activeApplications = await db.Applications.CountAsync(x => x.Status != ApplicationStatus.Rejected && x.Status != ApplicationStatus.Withdrawn, ct);
        var lastRun = await db.ScrapeRuns.AsNoTracking().OrderByDescending(x => x.StartedAt).FirstOrDefaultAsync(ct);
        var statuses = await db.Applications.GroupBy(x => x.Status).Select(x => new { status = x.Key, count = x.Count() }).ToListAsync(ct);
        return Results.Ok(new { newJobs, strongMatches, activeApplications, lastRun, applicationStatuses = statuses });
    }

    private static async Task<IResult> ListJobsAsync(CareerPilotDbContext db, string? search, JobReviewStatus? status, double? minimumScore, CancellationToken ct)
    {
        var query = db.Jobs.AsNoTracking().Include(x => x.SourceListings).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search) || x.Company.Contains(search) || x.Description.Contains(search));
        if (status is not null) query = query.Where(x => x.ReviewStatus == status);
        if (minimumScore is not null) query = query.Where(x => x.MatchScore >= minimumScore);
        return Results.Ok(await query.OrderByDescending(x => x.MatchScore).ThenByDescending(x => x.PostedAt).Take(250).ToListAsync(ct));
    }

    private static async Task<IResult> GetJobAsync(Guid id, CareerPilotDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().Include(x => x.SourceListings).SingleOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static async Task<IResult> ImportJobAsync(ManualJobRequest request, CareerPilotDbContext db, ScoringService scoring, CancellationToken ct)
    {
        var source = await db.CollectionSources.SingleOrDefaultAsync(x => x.Kind == "manual", ct);
        if (source is null)
        {
            source = new CollectionSource { Name = "Manual imports", Kind = "manual", SearchUrl = "manual://imports", Enabled = true };
            db.CollectionSources.Add(source);
            await db.SaveChangesAsync(ct);
        }
        var ingestion = new JobIngestionService(db, scoring);
        await ingestion.IngestAsync(source, [new(request.Url, request.Title, request.Company, request.Location, request.Description, request.Url, request.PostedAt)], ct);
        return Results.Accepted();
    }

    private static async Task<IResult> SetJobStatusAsync(Guid id, JobStatusRequest request, CareerPilotDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct);
        if (job is null) return Results.NotFound();
        job.ReviewStatus = request.Status;
        await db.SaveChangesAsync(ct);
        return Results.Ok(job);
    }

    private static async Task<IResult> SaveEvidenceAsync(EvidenceRequest request, CareerPilotDbContext db, CancellationToken ct)
    {
        var entity = request.Id is null ? new CareerEvidence() : await db.CareerEvidence.FindAsync([request.Id.Value], ct) ?? new CareerEvidence { Id = request.Id.Value };
        entity.Kind = request.Kind; entity.Title = request.Title.Trim(); entity.Organisation = request.Organisation.Trim();
        entity.Description = request.Description.Trim(); entity.SkillsCsv = request.SkillsCsv.Trim();
        entity.ApprovedForApplications = request.ApprovedForApplications; entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (db.Entry(entity).State == EntityState.Detached) db.CareerEvidence.Add(entity);
        await db.SaveChangesAsync(ct);
        return Results.Ok(entity);
    }

    private static async Task<IResult> DeleteEvidenceAsync(Guid id, CareerPilotDbContext db, CancellationToken ct)
    {
        var entity = await db.CareerEvidence.FindAsync([id], ct);
        if (entity is null) return Results.NotFound();
        db.Remove(entity); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> SavePreferencesAsync(PreferencesRequest request, CareerPilotDbContext db, ScoringService scoring, CancellationToken ct)
    {
        var entity = await db.SearchPreferences.SingleAsync(ct);
        entity.TargetTitlesCsv = request.TargetTitlesCsv; entity.TitleSynonymsJson = request.TitleSynonymsJson;
        entity.LocationsCsv = request.LocationsCsv; entity.WorkMode = request.WorkMode;
        entity.PositiveKeywordsCsv = request.PositiveKeywordsCsv; entity.NegativeKeywordsCsv = request.NegativeKeywordsCsv;
        entity.KnockoutKeywordsCsv = request.KnockoutKeywordsCsv; entity.MaxAgeDays = request.MaxAgeDays;
        entity.MinimumScore = request.MinimumScore; entity.UpdatedAt = DateTimeOffset.UtcNow;
        var evidence = await db.CareerEvidence.AsNoTracking().Where(x => x.ApprovedForApplications).ToListAsync(ct);
        foreach (var job in await db.Jobs.ToListAsync(ct))
        {
            var explanation = scoring.Score(job, entity, evidence);
            job.MatchScore = explanation.Score; job.MatchExplanationJson = ScoringService.Serialize(explanation);
        }
        await db.SaveChangesAsync(ct); return Results.Ok(entity);
    }

    private static async Task<IResult> ListApplicationsAsync(CareerPilotDbContext db, CancellationToken ct)
        => Results.Ok(await db.Applications.AsNoTracking().Include(x => x.Job).Include(x => x.Documents).OrderByDescending(x => x.UpdatedAt).ToListAsync(ct));

    private static async Task<IResult> CreateApplicationAsync(CreateApplicationRequest request, CareerPilotDbContext db, CancellationToken ct)
    {
        if (!await db.Jobs.AnyAsync(x => x.Id == request.JobId, ct)) return Results.NotFound();
        var existing = await db.Applications.SingleOrDefaultAsync(x => x.JobId == request.JobId, ct);
        if (existing is not null) return Results.Ok(existing);
        var application = new JobApplication { JobId = request.JobId, Notes = request.Notes ?? "" };
        db.Applications.Add(application);
        var job = await db.Jobs.FindAsync([request.JobId], ct); job!.ReviewStatus = JobReviewStatus.Saved;
        await db.SaveChangesAsync(ct); return Results.Created($"/api/applications/{application.Id}", application);
    }

    private static async Task<IResult> UpdateApplicationAsync(Guid id, UpdateApplicationRequest request, CareerPilotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var application = await db.Applications.FindAsync([id], ct);
        if (application is null) return Results.NotFound();
        ApplicationWorkflow.Transition(application, request.Status, time.GetUtcNow());
        application.Notes = request.Notes ?? application.Notes;
        if (request.Status == ApplicationStatus.Applied)
        {
            var job = await db.Jobs.FindAsync([application.JobId], ct); if (job is not null) job.ReviewStatus = JobReviewStatus.Applied;
        }
        await db.SaveChangesAsync(ct); return Results.Ok(application);
    }

    private static async Task<IResult> CreateDocumentsAsync(Guid id, ApplicationPackageInput request, CareerPilotDbContext db, DocumentService documents, IConfiguration configuration, CancellationToken ct)
    {
        var application = await db.Applications.Include(x => x.Job).Include(x => x.Documents).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null) return Results.NotFound();
        var allEvidence = await db.CareerEvidence.AsNoTracking().ToListAsync(ct);
        var evidence = EvidenceGuard.RequireApproved(request.EvidenceIds, allEvidence);
        var root = Path.GetFullPath(configuration["Storage:DocumentsPath"] ?? "data/documents");
        var folder = Path.Combine(root, id.ToString("N")); Directory.CreateDirectory(folder);
        var created = new List<DocumentVersion>();
        var outputs = new[]
        {
            await documents.CreateResumeDocxAsync(application, request, evidence, ct),
            documents.CreateCoverLetterPdf(application, request, evidence)
        };
        foreach (var output in outputs)
        {
            var kind = output.MimeType == "application/pdf" ? "cover-letter" : "resume";
            var version = application.Documents.Where(x => x.Kind == kind).Select(x => x.Version).DefaultIfEmpty().Max() + 1;
            var fileName = $"v{version}-{output.FileName}";
            var path = Path.Combine(folder, fileName);
            await File.WriteAllBytesAsync(path, output.Bytes, ct);
            var entity = new DocumentVersion { ApplicationId = id, Kind = kind, Version = version, FileName = output.FileName,
                RelativePath = Path.GetRelativePath(root, path), MimeType = output.MimeType, EvidenceIdsJson = DocumentService.EvidenceJson(request.EvidenceIds) };
            db.DocumentVersions.Add(entity); created.Add(entity);
        }
        await db.SaveChangesAsync(ct); return Results.Ok(created);
    }

    private static async Task<IResult> DownloadDocumentAsync(Guid id, CareerPilotDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var document = await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (document is null) return Results.NotFound();
        var root = Path.GetFullPath(configuration["Storage:DocumentsPath"] ?? "data/documents");
        var path = Path.GetFullPath(Path.Combine(root, document.RelativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return Results.NotFound();
        return Results.File(path, document.MimeType, document.FileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> SaveSourceAsync(SourceRequest request, CareerPilotDbContext db, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.SearchUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            return Results.BadRequest(new { error = "Source URL must be an absolute HTTP or HTTPS URL." });
        var entity = request.Id is null ? new CollectionSource() : await db.CollectionSources.FindAsync([request.Id.Value], ct) ?? new CollectionSource { Id = request.Id.Value };
        entity.Name = request.Name.Trim(); entity.Kind = request.UseBrowser ? "browser-jsonld" : "jsonld"; entity.SearchUrl = url.ToString();
        entity.UseBrowser = request.UseBrowser; entity.Enabled = request.Enabled; entity.RequestDelayMs = Math.Clamp(request.RequestDelayMs, 500, 60_000);
        entity.MaximumPages = Math.Clamp(request.MaximumPages, 1, 10);
        if (db.Entry(entity).State == EntityState.Detached) db.CollectionSources.Add(entity);
        await db.SaveChangesAsync(ct); return Results.Ok(entity);
    }

    private static async Task<IResult> DeleteSourceAsync(Guid id, CareerPilotDbContext db, CancellationToken ct)
    {
        var entity = await db.CollectionSources.FindAsync([id], ct); if (entity is null) return Results.NotFound();
        entity.Enabled = false; await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> QueueScrapeAsync(CareerPilotDbContext db, CancellationToken ct)
    {
        if (await db.ScrapeRequests.AnyAsync(x => x.Status == ScrapeRequestStatus.Pending || x.Status == ScrapeRequestStatus.Running, ct))
            return Results.Conflict(new { error = "A collection run is already pending or running." });
        var request = new ScrapeRequest(); db.ScrapeRequests.Add(request); await db.SaveChangesAsync(ct);
        return Results.Accepted($"/api/scrapes/requests/{request.Id}", request);
    }

    private static string SafeReturnUrl(string? url) => !string.IsNullOrWhiteSpace(url) && Uri.IsWellFormedUriString(url, UriKind.Relative) ? url : "/";
}

public sealed record ManualJobRequest(string Title, string Company, string Location, string Description, string Url, DateTimeOffset? PostedAt);
public sealed record JobStatusRequest(JobReviewStatus Status);
public sealed record EvidenceRequest(Guid? Id, EvidenceKind Kind, string Title, string Organisation, string Description, string SkillsCsv, bool ApprovedForApplications);
public sealed record PreferencesRequest(string TargetTitlesCsv, string TitleSynonymsJson, string LocationsCsv, WorkMode WorkMode,
    string PositiveKeywordsCsv, string NegativeKeywordsCsv, string KnockoutKeywordsCsv, int MaxAgeDays, double MinimumScore);
public sealed record CreateApplicationRequest(Guid JobId, string? Notes);
public sealed record UpdateApplicationRequest(ApplicationStatus Status, string? Notes);
public sealed record SourceRequest(Guid? Id, string Name, string SearchUrl, bool UseBrowser, bool Enabled, int RequestDelayMs, int MaximumPages);
