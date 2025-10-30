namespace DynamicFormsRazor.Models;

public class FieldDefinition
{
    public string Name { get; set; } = default!; // key
    public string Label { get; set; } = default!;
    public FieldType Type { get; set; } = FieldType.Text;
    public string? Placeholder { get; set; }
    public string? HelpText { get; set; }
    public string[]? Options { get; set; } // for Select/Radio
    public ValidationRule Validation { get; set; } = new();
}
