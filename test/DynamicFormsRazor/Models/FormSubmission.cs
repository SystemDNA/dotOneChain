using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DynamicFormsRazor.Models;

public class FormSubmission
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>FormDefinition Id the user filled.</summary>
    public string FormDefinitionId { get; set; } = default!;

    /// <summary>Stable key for grouping versions.</summary>
    public string FormKey { get; set; } = default!;

    /// <summary>Version of the definition used for this submission.</summary>
    public int FormVersion { get; set; }

    /// <summary>Display name of the form at submission time.</summary>
    public string FormDefinitionName { get; set; } = default!;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional metadata.</summary>
    public string? SubmittedBy { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Arbitrary JSON payload stored as a BsonDocument.</summary>
    public BsonDocument Data { get; set; } = new();
}
