using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicFormsRazor.Models;

public class FormDefinition
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Stable logical key (e.g., "insurance-policy"). All versions share this key.
    /// </summary>
    public string FormKey { get; set; } = default!;

    /// <summary>
    /// Human-friendly display name.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Monotonic version number starting at 1.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Exactly one document per FormKey is current at any time.
    /// </summary>
    public bool IsCurrent { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Single JSON doc where each Section embeds its own Fields
    public List<SectionDefinition> Sections { get; set; } = new();
}
