using System.Text.Json;
using CareerPilot.Domain;

namespace CareerPilot.Application;

public sealed class ScoringService
{
    public MatchExplanation Score(Job job, SearchPreferences preferences, IEnumerable<CareerEvidence> evidence)
    {
        var haystack = $"{job.Title} {job.Description}".ToLowerInvariant();
        var factors = new List<MatchFactor>();
        var missing = new List<string>();

        var knockout = Csv(preferences.KnockoutKeywordsCsv).FirstOrDefault(x => haystack.Contains(x, StringComparison.OrdinalIgnoreCase));
        if (knockout is not null)
        {
            factors.Add(new("Knockout", -100, $"Contains excluded term '{knockout}'."));
            return new(0, true, factors, missing);
        }

        var titles = ExpandTitles(preferences);
        var titleMatches = titles.Where(x => job.Title.Contains(x, StringComparison.OrdinalIgnoreCase)).ToArray();
        var titlePoints = titleMatches.Length > 0 ? 35 : 0;
        factors.Add(new("Title", titlePoints, titleMatches.Length > 0
            ? $"Matched {string.Join(", ", titleMatches)}."
            : "No preferred title matched."));

        var approvedSkills = evidence.Where(x => x.ApprovedForApplications)
            .SelectMany(x => Csv(x.SkillsCsv)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var matchedSkills = approvedSkills.Where(x => haystack.Contains(x, StringComparison.OrdinalIgnoreCase)).ToArray();
        var skillPoints = Math.Min(30, matchedSkills.Length * 5);
        factors.Add(new("Evidence-backed skills", skillPoints,
            matchedSkills.Length > 0 ? $"Matched {string.Join(", ", matchedSkills)}." : "No approved skills matched."));

        var positives = Csv(preferences.PositiveKeywordsCsv);
        var matchedPositive = positives.Where(x => haystack.Contains(x, StringComparison.OrdinalIgnoreCase)).ToArray();
        var positivePoints = Math.Min(15, matchedPositive.Length * 3);
        factors.Add(new("Positive keywords", positivePoints,
            matchedPositive.Length > 0 ? $"Matched {string.Join(", ", matchedPositive)}." : "No positive keywords matched."));

        var negatives = Csv(preferences.NegativeKeywordsCsv);
        var matchedNegative = negatives.Where(x => haystack.Contains(x, StringComparison.OrdinalIgnoreCase)).ToArray();
        var negativePoints = -Math.Min(30, matchedNegative.Length * 10);
        factors.Add(new("Negative keywords", negativePoints,
            matchedNegative.Length > 0 ? $"Matched {string.Join(", ", matchedNegative)}." : "No negative keywords matched."));

        var locations = Csv(preferences.LocationsCsv);
        var locationMatched = locations.Length == 0 || locations.Any(x => job.Location.Contains(x, StringComparison.OrdinalIgnoreCase));
        factors.Add(new("Location", locationMatched ? 10 : -10,
            locationMatched ? "Location is acceptable." : "Location does not match preferences."));

        var ageDays = job.PostedAt is null ? 0 : Math.Max(0, (DateTimeOffset.UtcNow - job.PostedAt.Value).TotalDays);
        var recencyPoints = ageDays <= 2 ? 10 : ageDays <= 7 ? 7 : ageDays <= preferences.MaxAgeDays ? 3 : -10;
        factors.Add(new("Recency", recencyPoints, job.PostedAt is null ? "Posting date unavailable." : $"Posted {Math.Floor(ageDays)} days ago."));

        foreach (var preferred in titles.Where(x => !haystack.Contains(x, StringComparison.OrdinalIgnoreCase)).Take(3))
            missing.Add(preferred);

        return new(Math.Clamp(factors.Sum(x => x.Points), 0, 100), false, factors, missing);
    }

    public static string Serialize(MatchExplanation explanation) => JsonSerializer.Serialize(explanation);

    private static string[] ExpandTitles(SearchPreferences preferences)
    {
        var result = new HashSet<string>(Csv(preferences.TargetTitlesCsv), StringComparer.OrdinalIgnoreCase);
        try
        {
            var synonyms = JsonSerializer.Deserialize<Dictionary<string, string[]>>(preferences.TitleSynonymsJson) ?? [];
            foreach (var item in synonyms)
            {
                result.Add(item.Key);
                foreach (var synonym in item.Value) result.Add(synonym);
            }
        }
        catch (JsonException) { }
        return result.Where(x => x.Length > 1).ToArray();
    }

    internal static string[] Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
