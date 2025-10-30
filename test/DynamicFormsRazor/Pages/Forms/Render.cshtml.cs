using DynamicFormsRazor.Models;
using DynamicFormsRazor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;

namespace DynamicFormsRazor.Pages.Forms;

public class RenderModel : PageModel
{
    private readonly FormDefinitionRepository _defs;
    private readonly FormSubmissionRepository _subs;
    private readonly SchemaValidator _validator;

    public RenderModel(FormDefinitionRepository defs, FormSubmissionRepository subs, SchemaValidator validator)
    {
        _defs = defs;
        _subs = subs;
        _validator = validator;
    }

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    public FormDefinition? Definition { get; set; }

    [BindProperty]
    public Dictionary<string, string?> Answers { get; set; } = new();

    public Dictionary<string, string> Errors { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Id)) return RedirectToPage("/Forms/Index");
        Definition = await _defs.GetAsync(Id!);
        if (Definition is null) return RedirectToPage("/Forms/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Id)) return RedirectToPage("/Forms/Index");
        Definition = await _defs.GetAsync(Id!);
        if (Definition is null) return RedirectToPage("/Forms/Index");

        // Collect values
        var input = new Dictionary<string, string?>();
        foreach (var s in Definition.Sections)
        {
            foreach (var f in s.Fields)
                input[f.Name] = Request.Form[f.Name].FirstOrDefault();
        }

        var errors = _validator.Validate(Definition, input.ToDictionary(k => k.Key, v => v.Value));
        Errors = errors;
        Answers = input;

        if (errors.Count > 0)
        {
            ModelState.Clear();
            return Page();
        }

        // Save submission as raw JSON with metadata
        var doc = new BsonDocument();
        foreach (var kv in input)
            doc[kv.Key] = kv.Value ??"";

        var sub = new FormSubmission
        {
            FormDefinitionId = Definition.Id!,
            FormKey = Definition.FormKey,
            FormVersion = Definition.Version,
            FormDefinitionName = Definition.Name,
            Data = doc,
            SubmittedAtUtc = DateTime.UtcNow,
            SubmittedBy = User?.Identity?.IsAuthenticated == true ? User.Identity?.Name : null,
            ClientIp = HttpContext.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].FirstOrDefault()
        };

        await _subs.InsertAsync(sub);

        TempData["Toast"] = "Submission saved.";
        return RedirectToPage("/Forms/Index");
    }
}
