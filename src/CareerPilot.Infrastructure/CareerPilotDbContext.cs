using CareerPilot.Domain;
using Microsoft.EntityFrameworkCore;

namespace CareerPilot.Infrastructure;

public sealed class CareerPilotDbContext(DbContextOptions<CareerPilotDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<SourceListing> SourceListings => Set<SourceListing>();
    public DbSet<CareerEvidence> CareerEvidence => Set<CareerEvidence>();
    public DbSet<SearchPreferences> SearchPreferences => Set<SearchPreferences>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<CollectionSource> CollectionSources => Set<CollectionSource>();
    public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();
    public DbSet<ScrapeRequest> ScrapeRequests => Set<ScrapeRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasIndex(x => x.CanonicalUrl).IsUnique();
            entity.HasIndex(x => new { x.ReviewStatus, x.MatchScore });
            entity.Property(x => x.Title).HasMaxLength(300);
            entity.Property(x => x.Company).HasMaxLength(300);
            entity.Property(x => x.CanonicalUrl).HasMaxLength(2000);
        });
        modelBuilder.Entity<SourceListing>(entity =>
        {
            entity.HasIndex(x => new { x.CollectionSourceId, x.ExternalId }).IsUnique();
            entity.HasOne(x => x.Job).WithMany(x => x.SourceListings).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasIndex(x => x.JobId).IsUnique();
            entity.HasOne(x => x.Job).WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DocumentVersion>(entity =>
        {
            entity.HasIndex(x => new { x.ApplicationId, x.Kind, x.Version }).IsUnique();
            entity.HasOne(x => x.Application).WithMany(x => x.Documents).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ScrapeRun>().HasIndex(x => x.RunKey).IsUnique();
        modelBuilder.Entity<SearchPreferences>().HasData(new SearchPreferences());
    }
}
