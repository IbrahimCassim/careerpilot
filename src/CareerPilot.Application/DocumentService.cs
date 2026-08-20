using System.Text.Json;
using CareerPilot.Domain;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CareerPilot.Application;

public sealed record ApplicationPackageInput(
    string CandidateName,
    string Email,
    string Phone,
    string Summary,
    IReadOnlyList<Guid> EvidenceIds,
    string CoverLetterBody);

public sealed class DocumentService
{
    public async Task<(string FileName, string MimeType, byte[] Bytes)> CreateResumeDocxAsync(
        JobApplication application, ApplicationPackageInput input, IReadOnlyList<CareerEvidence> evidence, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;
            body.Append(Heading(input.CandidateName, 32));
            body.Append(Paragraph($"{input.Email} · {input.Phone}"));
            body.Append(Heading($"Target: {application.Job.Title} — {application.Job.Company}", 22));
            body.Append(Heading("Professional summary", 22));
            body.Append(Paragraph(input.Summary));
            body.Append(Heading("Relevant experience and evidence", 22));
            foreach (var item in evidence)
            {
                body.Append(Heading(item.Title, 20));
                body.Append(Paragraph(string.IsNullOrWhiteSpace(item.Organisation) ? item.Kind.ToString() : $"{item.Organisation} · {item.Kind}"));
                body.Append(Bullet(item.Description));
                if (!string.IsNullOrWhiteSpace(item.SkillsCsv)) body.Append(Paragraph($"Skills: {item.SkillsCsv}"));
            }
            main.Document.Save();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return ($"resume-{Slug(application.Job.Company)}-{Slug(application.Job.Title)}.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", stream.ToArray());
    }

    public (string FileName, string MimeType, byte[] Bytes) CreateCoverLetterPdf(
        JobApplication application, ApplicationPackageInput input, IReadOnlyList<CareerEvidence> evidence)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var evidenceTitles = string.Join(", ", evidence.Select(x => x.Title));
        var bytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));
                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text(input.CandidateName).FontSize(20).Bold();
                    column.Item().Text($"{input.Email}  |  {input.Phone}").FontColor(Colors.Grey.Darken2);
                    column.Item().PaddingTop(20).Text($"Re: {application.Job.Title} at {application.Job.Company}").Bold();
                    column.Item().Text(input.CoverLetterBody);
                    column.Item().PaddingTop(10).Text($"Evidence used: {evidenceTitles}").FontSize(8).FontColor(Colors.Grey.Medium);
                });
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("CareerPilot application package · ");
                    x.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
        return ($"cover-letter-{Slug(application.Job.Company)}-{Slug(application.Job.Title)}.pdf", "application/pdf", bytes);
    }

    public static string EvidenceJson(IEnumerable<Guid> ids) => JsonSerializer.Serialize(ids.Distinct());

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph Heading(string text, int halfPoints)
        => new(new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
            new Run(new RunProperties(new Bold(), new FontSize { Val = halfPoints.ToString() }), new Text(text)));

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph Paragraph(string text)
        => new(new ParagraphProperties(new SpacingBetweenLines { After = "120" }), new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static DocumentFormat.OpenXml.Wordprocessing.Paragraph Bullet(string text)
        => new(new ParagraphProperties(new Indentation { Left = "360", Hanging = "180" }), new Run(new Text($"• {text}")));

    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '-').ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries)).Take(80).ToArray() is var result
            ? new string(result) : "document";
    }
}
