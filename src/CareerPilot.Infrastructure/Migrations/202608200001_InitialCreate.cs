using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerPilot.Infrastructure.Migrations;

[Migration("202608200001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS CareerEvidence (
              Id TEXT NOT NULL PRIMARY KEY, Kind INTEGER NOT NULL, Title TEXT NOT NULL, Organisation TEXT NOT NULL,
              Description TEXT NOT NULL, SkillsCsv TEXT NOT NULL, StartedOn TEXT NULL, EndedOn TEXT NULL,
              ApprovedForApplications INTEGER NOT NULL, UpdatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS CollectionSources (
              Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, Kind TEXT NOT NULL, SearchUrl TEXT NOT NULL,
              UseBrowser INTEGER NOT NULL, Enabled INTEGER NOT NULL, RequestDelayMs INTEGER NOT NULL,
              MaximumPages INTEGER NOT NULL, LastSucceededAt TEXT NULL, LastError TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Jobs (
              Id TEXT NOT NULL PRIMARY KEY, Title TEXT NOT NULL, Company TEXT NOT NULL, Location TEXT NOT NULL,
              WorkMode INTEGER NOT NULL, Description TEXT NOT NULL, CanonicalUrl TEXT NOT NULL, PostedAt TEXT NULL,
              FirstSeenAt TEXT NOT NULL, LastSeenAt TEXT NOT NULL, ClosedAt TEXT NULL, ReviewStatus INTEGER NOT NULL,
              MatchScore REAL NOT NULL, MatchExplanationJson TEXT NOT NULL, ContentFingerprint TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Jobs_CanonicalUrl ON Jobs (CanonicalUrl);
            CREATE INDEX IF NOT EXISTS IX_Jobs_ReviewStatus_MatchScore ON Jobs (ReviewStatus, MatchScore);
            CREATE TABLE IF NOT EXISTS SearchPreferences (
              Id INTEGER NOT NULL PRIMARY KEY, TargetTitlesCsv TEXT NOT NULL, TitleSynonymsJson TEXT NOT NULL,
              LocationsCsv TEXT NOT NULL, WorkMode INTEGER NOT NULL, PositiveKeywordsCsv TEXT NOT NULL,
              NegativeKeywordsCsv TEXT NOT NULL, KnockoutKeywordsCsv TEXT NOT NULL, MaxAgeDays INTEGER NOT NULL,
              MinimumScore REAL NOT NULL, UpdatedAt TEXT NOT NULL
            );
            INSERT OR IGNORE INTO SearchPreferences
              (Id, TargetTitlesCsv, TitleSynonymsJson, LocationsCsv, WorkMode, PositiveKeywordsCsv,
               NegativeKeywordsCsv, KnockoutKeywordsCsv, MaxAgeDays, MinimumScore, UpdatedAt)
              VALUES (1, 'Software Engineer,Developer', '{}', 'Australia', 0, '', '', '', 30, 45, CURRENT_TIMESTAMP);
            CREATE TABLE IF NOT EXISTS ScrapeRequests (
              Id TEXT NOT NULL PRIMARY KEY, Status INTEGER NOT NULL, RequestedAt TEXT NOT NULL, CompletedAt TEXT NULL, Error TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Applications (
              Id TEXT NOT NULL PRIMARY KEY, JobId TEXT NOT NULL, Status INTEGER NOT NULL, Notes TEXT NOT NULL,
              CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, AppliedAt TEXT NULL,
              FOREIGN KEY (JobId) REFERENCES Jobs (Id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Applications_JobId ON Applications (JobId);
            CREATE TABLE IF NOT EXISTS SourceListings (
              Id TEXT NOT NULL PRIMARY KEY, JobId TEXT NOT NULL, CollectionSourceId TEXT NOT NULL, ExternalId TEXT NOT NULL,
              SourceUrl TEXT NOT NULL, FirstSeenAt TEXT NOT NULL, LastSeenAt TEXT NOT NULL,
              FOREIGN KEY (JobId) REFERENCES Jobs (Id) ON DELETE CASCADE,
              FOREIGN KEY (CollectionSourceId) REFERENCES CollectionSources (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_SourceListings_CollectionSourceId_ExternalId ON SourceListings (CollectionSourceId, ExternalId);
            CREATE TABLE IF NOT EXISTS ScrapeRuns (
              Id TEXT NOT NULL PRIMARY KEY, CollectionSourceId TEXT NULL, RunKey TEXT NOT NULL, Status INTEGER NOT NULL,
              StartedAt TEXT NOT NULL, CompletedAt TEXT NULL, DiscoveredCount INTEGER NOT NULL, AddedCount INTEGER NOT NULL,
              UpdatedCount INTEGER NOT NULL, Error TEXT NOT NULL,
              FOREIGN KEY (CollectionSourceId) REFERENCES CollectionSources (Id) ON DELETE SET NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ScrapeRuns_RunKey ON ScrapeRuns (RunKey);
            CREATE TABLE IF NOT EXISTS DocumentVersions (
              Id TEXT NOT NULL PRIMARY KEY, ApplicationId TEXT NOT NULL, Kind TEXT NOT NULL, Version INTEGER NOT NULL,
              FileName TEXT NOT NULL, RelativePath TEXT NOT NULL, MimeType TEXT NOT NULL, EvidenceIdsJson TEXT NOT NULL,
              CreatedAt TEXT NOT NULL, FOREIGN KEY (ApplicationId) REFERENCES Applications (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DocumentVersions_ApplicationId_Kind_Version ON DocumentVersions (ApplicationId, Kind, Version);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS DocumentVersions;
            DROP TABLE IF EXISTS ScrapeRuns;
            DROP TABLE IF EXISTS SourceListings;
            DROP TABLE IF EXISTS Applications;
            DROP TABLE IF EXISTS ScrapeRequests;
            DROP TABLE IF EXISTS SearchPreferences;
            DROP TABLE IF EXISTS Jobs;
            DROP TABLE IF EXISTS CollectionSources;
            DROP TABLE IF EXISTS CareerEvidence;
            """);
    }
}
