using DynamicFormsRazor.Models;
using DynamicFormsRazor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DynamicFormsRazor.Pages.Forms;

public class SubmissionsModel : PageModel
{
    private readonly FormSubmissionRepository _subs;
    private readonly FormDefinitionRepository _defs;

    public SubmissionsModel(FormSubmissionRepository subs, FormDefinitionRepository defs)
    {
        _subs = subs;
        _defs = defs;
    }

    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Version { get; set; }

    public List<FormSubmission> Items { get; set; } = new();
    public FormDefinition? Current { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Key))
            return RedirectToPage("/Forms/Index");

        Items = await _subs.ListByKeyAsync(Key!, Version);
        Current = await _defs.GetCurrentByKeyAsync(Key!);
        return Page();
    }
}
