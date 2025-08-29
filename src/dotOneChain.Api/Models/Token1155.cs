using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotOneChain.Api.Models;

public class Token1155
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string TokenId { get; set; } = Guid.NewGuid().ToString("N"); // now a normal field

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MetadataCid { get; set; } = string.Empty;
    public long MaxSupply { get; set; } = 0;
    public long TotalMinted { get; set; } = 0;
    public long TotalBurned { get; set; } = 0;
    public bool Transferable { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
