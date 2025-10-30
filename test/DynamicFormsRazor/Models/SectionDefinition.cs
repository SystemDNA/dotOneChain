namespace DynamicFormsRazor.Models;

public class SectionDefinition
{
    public string Id { get; set; } = default!; // slug
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public int Order { get; set; } = 0;
    public List<FieldDefinition> Fields { get; set; } = new();
}
