namespace DynamicFormsRazor.Models;

public class ValidationRule
{
    public bool Required { get; set; } = false;
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? Pattern { get; set; } // Regex
    public string? CustomMessage { get; set; }
}
