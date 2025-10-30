using DynamicFormsRazor.Models;
using DynamicFormsRazor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DynamicFormsRazor.Pages.Forms;

public class HistoryModel : PageModel
{
    private readonly FormDefinitionRepository _repo;

    public HistoryModel(FormDefinitionRepository repo)
    {
        _repo = repo;
    }

    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    public List<FormDefinition> Versions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Key))
            return RedirectToPage("/Forms/Index");

        Versions = await _repo.ListVersionsAsync(Key);
        if (Versions.Count == 0)
            return RedirectToPage("/Forms/Index");

        return Page();
    }
}
