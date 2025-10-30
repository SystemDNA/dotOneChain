using DynamicFormsRazor.Models;
using DynamicFormsRazor.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DynamicFormsRazor.Pages;

public class IndexModel : PageModel
{
    private readonly FormDefinitionRepository _defs;
    private readonly FormSubmissionRepository _subs;

    public IndexModel(FormDefinitionRepository defs, FormSubmissionRepository subs)
    {
        _defs = defs;
        _subs = subs;
    }

    // KPI tiles
    public int CurrentDefinitionsCount { get; set; }
    public int TotalVersionsCount { get; set; }
    public long TotalSubmissionsCount { get; set; }

    // Lists
    public List<FormDefinition> CurrentDefinitions { get; set; } = new();
    public List<FormDefinition> LatestVersionsPerKey { get; set; } = new(); // alias of current
    public List<FormSubmission> RecentSubmissions { get; set; } = new();

    // Quick lookup: submissions per key
    public Dictionary<string, long> SubmissionsByKey { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Current versions only
        CurrentDefinitions = await _defs.ListCurrentsAsync();
        CurrentDefinitionsCount = CurrentDefinitions.Count;

        // Total versions = sum of versions for each key
        // We can estimate by summing ListVersionsAsync for each FormKey (for small N) or run a Mongo aggregation.
        // Simple approach here:
        var formKeys = CurrentDefinitions.Select(d => d.FormKey).Distinct().ToList();
        int totalVersions = 0;
        foreach (var k in formKeys)
        {
            var versions = await _defs.ListVersionsAsync(k);
            totalVersions += versions.Count;
        }
        TotalVersionsCount = totalVersions;

        // Submissions
        RecentSubmissions = await _subs.ListRecentAsync(15);
        SubmissionsByKey = await _subs.CountByFormKeyAsync();
        TotalSubmissionsCount = SubmissionsByKey.Values.Sum();

        // Alias for clarity in view
        LatestVersionsPerKey = CurrentDefinitions;
    }
}
