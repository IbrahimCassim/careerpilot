using System.Text.Json;
using AngleSharp.Html.Parser;
using CareerPilot.Domain;
using Microsoft.Playwright;

namespace CareerPilot.Infrastructure;

public sealed class JsonLdParser
{
    private readonly HtmlParser _parser = new();

    public async Task<IReadOnlyList<DiscoveredJob>> ParseAsync(string html, Uri pageUri, CancellationToken cancellationToken)
    {
        var document = await _parser.ParseDocumentAsync(html, cancellationToken);
        var jobs = new List<DiscoveredJob>();
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                Extract(json.RootElement, pageUri, jobs);
            }
            catch (JsonException) { }
        }
        return jobs.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
    }

    private static void Extract(JsonElement node, Uri pageUri, List<DiscoveredJob> jobs)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray()) Extract(child, pageUri, jobs);
            return;
        }
        if (node.ValueKind != JsonValueKind.Object) return;
        if (node.TryGetProperty("@graph", out var graph)) Extract(graph, pageUri, jobs);
        var type = Text(node, "@type");
        if (!string.Equals(type, "JobPosting", StringComparison.OrdinalIgnoreCase)) return;

        var title = Text(node, "title");
        var description = Text(node, "description");
        var company = node.TryGetProperty("hiringOrganization", out var org) ? Text(org, "name") : "Unknown company";
        var externalId = node.TryGetProperty("identifier", out var identifier) ? Text(identifier, "value") : "";
        var rawUrl = Text(node, "url");
        var url = Uri.TryCreate(rawUrl, UriKind.Absolute, out var absolute) ? absolute.ToString() : pageUri.ToString();
        var location = Location(node);
        var postedAt = DateTimeOffset.TryParse(Text(node, "datePosted"), out var posted) ? posted : null;
        if (string.IsNullOrWhiteSpace(title)) return;
        jobs.Add(new(string.IsNullOrWhiteSpace(externalId) ? url : externalId, title, company, location,
            StripHtml(description), url, postedAt, ParseWorkMode(node)));
    }

    private static string Location(JsonElement node)
    {
        if (!node.TryGetProperty("jobLocation", out var location)) return Text(node, "jobLocationType");
        if (location.ValueKind == JsonValueKind.Array) location = location.EnumerateArray().FirstOrDefault();
        if (!location.TryGetProperty("address", out var address)) return "";
        return string.Join(", ", new[] { Text(address, "addressLocality"), Text(address, "addressRegion"), Text(address, "addressCountry") }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static WorkMode ParseWorkMode(JsonElement node)
    {
        var value = Text(node, "jobLocationType");
        if (value.Contains("TELECOMMUTE", StringComparison.OrdinalIgnoreCase)) return WorkMode.Remote;
        var description = Text(node, "description");
        if (description.Contains("hybrid", StringComparison.OrdinalIgnoreCase)) return WorkMode.Hybrid;
        return WorkMode.Any;
    }

    private static string Text(JsonElement node, string property)
        => node.ValueKind == JsonValueKind.Object && node.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString()
            : "";

    private static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var text = System.Text.RegularExpressions.Regex.Replace(input, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ")).Trim();
    }
}

public sealed class JsonLdHttpAdapter(HttpClient httpClient, JsonLdParser parser) : IJobSourceAdapter
{
    public string Kind => "jsonld";
    public async Task<IReadOnlyList<DiscoveredJob>> DiscoverAsync(CollectionSource source, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(source.SearchUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return await parser.ParseAsync(html, new Uri(source.SearchUrl), cancellationToken);
    }
}

public sealed class JsonLdBrowserAdapter(JsonLdParser parser) : IJobSourceAdapter
{
    public string Kind => "browser-jsonld";
    public async Task<IReadOnlyList<DiscoveredJob>> DiscoverAsync(CollectionSource source, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var context = await browser.NewContextAsync(new() { ServiceWorkers = ServiceWorkerPolicy.Block });
        var page = await context.NewPageAsync();
        await page.RouteAsync("**/*", async route =>
        {
            if (route.Request.ResourceType is "image" or "media" or "font") await route.AbortAsync();
            else await route.ContinueAsync();
        });
        await page.GotoAsync(source.SearchUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45_000 });
        var html = await page.ContentAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return await parser.ParseAsync(html, new Uri(source.SearchUrl), cancellationToken);
    }
}
