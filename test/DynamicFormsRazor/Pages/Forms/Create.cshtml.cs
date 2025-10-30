using DynamicFormsRazor.Models;
using DynamicFormsRazor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DynamicFormsRazor.Pages.Forms;

public class CreateModel : PageModel
{
    private readonly FormDefinitionRepository _repo;

    public CreateModel(FormDefinitionRepository repo)
    {
        _repo = repo;
    }

    // When cloning from existing current version by FormKey
    [BindProperty(SupportsGet = true)]
    public string? FromKey { get; set; }

    // When cloning from a specific version by Id (e.g., via History)
    [BindProperty(SupportsGet = true)]
    public string? FromId { get; set; }

    // Stable logical key shared by all versions
    [BindProperty]
    public string FormKey { get; set; } = "insurance-policy";

    // Display name
    [BindProperty]
    public string FormName { get; set; } = "Insurance Policy Application";

    // JSON for Sections[] with embedded Fields[]
    [BindProperty]
    public string DefinitionJson { get; set; } = "";

    public string? PrefillMessage { get; set; }

    public async Task OnGetAsync()
    {
        // If user requested to clone from an existing definition…
        if (!string.IsNullOrWhiteSpace(FromId))
        {
            var def = await _repo.GetAsync(FromId!);
            if (def is not null)
            {
                FormKey = def.FormKey;
                FormName = def.Name;
                DefinitionJson = JsonSerializer.Serialize(def.Sections, new JsonSerializerOptions { WriteIndented = true });
                PrefillMessage = $"Prefilled from version v{def.Version} (key: {def.FormKey}).";
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(FromKey))
        {
            var def = await _repo.GetCurrentByKeyAsync(FromKey!);
            if (def is not null)
            {
                FormKey = def.FormKey;
                FormName = def.Name;
                DefinitionJson = JsonSerializer.Serialize(def.Sections, new JsonSerializerOptions { WriteIndented = true });
                PrefillMessage = $"Prefilled from current version v{def.Version} (key: {def.FormKey}).";
                return;
            }
        }

        // Otherwise seed minimal starter to avoid empty editor
        if (string.IsNullOrWhiteSpace(DefinitionJson))
        {
            var seed = new List<SectionDefinition> {
                new SectionDefinition {
                    Id = "basic", Title = "Basic Info", Description = "Your details", Order = 1,
                    Fields = new List<FieldDefinition> {
                        new FieldDefinition { Name = "fullName", Label = "Full Name", Type = FieldType.Text,
                            Placeholder = "e.g. Rajasekhar D.", Validation = new ValidationRule { Required = true, MinLength = 3, MaxLength = 80 } },
                        new FieldDefinition { Name = "email", Label = "Email", Type = FieldType.Email,
                            Placeholder = "name@example.com", Validation = new ValidationRule { Required = true } }
                    }
                }
            };
            DefinitionJson = JsonSerializer.Serialize(seed, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FormKey))
            {
                ModelState.AddModelError(string.Empty, "Form key is required.");
                return Page();
            }

            FormKey = Slug(FormKey);

            if (string.IsNullOrWhiteSpace(FormName))
            {
                ModelState.AddModelError(string.Empty, "Form name is required.");
                return Page();
            }

            var sections = JsonSerializer.Deserialize<List<SectionDefinition>>(DefinitionJson) ?? new();
            if (sections.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Add at least one section with fields.");
                return Page();
            }

            // sanity checks
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in sections)
            {
                if (string.IsNullOrWhiteSpace(s.Id))
                    return Error("A section is missing its 'Id'.");
                if (!seen.Add(s.Id))
                    return Error($"Duplicate section Id '{s.Id}'.");
                if (s.Fields is null || s.Fields.Count == 0)
                    return Error($"Section '{s.Title ?? s.Id}' must contain at least one field.");

                var local = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in s.Fields)
                {
                    if (string.IsNullOrWhiteSpace(f.Name))
                        return Error($"A field in section '{s.Title ?? s.Id}' is missing its 'Name'.");
                    if (!local.Add(f.Name))
                        return Error($"Duplicate field name '{f.Name}' within section '{s.Title ?? s.Id}'.");
                }
            }

            var def = new FormDefinition
            {
                FormKey = FormKey,
                Name = FormName.Trim(),
                Sections = sections.OrderBy(s => s.Order).ToList()
            };

            await _repo.InsertNewVersionAsync(def);
            TempData["Toast"] = $"Published v{def.Version} for '{def.FormKey}'.";
            return RedirectToPage("/Forms/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Invalid JSON: {ex.Message}");
            return Page();
        }

        IActionResult Error(string message)
        {
            ModelState.AddModelError(string.Empty, message);
            return Page();
        }

        static string Slug(string v)
        {
            v = v.Trim().ToLowerInvariant();
            v = Regex.Replace(v, @"[^a-z0-9\-]+", "-");
            v = Regex.Replace(v, @"-+", "-").Trim('-');
            return v;
        }
    }
}
