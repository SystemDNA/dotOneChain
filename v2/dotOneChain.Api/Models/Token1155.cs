using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotOneChain.Api.Models;

public class Token1155
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string TokenId { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // The Object JSON *is* the NFT. These fields point to the current JSON asset.
    public string CurrentObjectCid { get; set; } = string.Empty;
    public int CurrentVersion { get; set; } = 0;
    public List<AssetVersion> ObjectVersions { get; set; } = new();

    public long MaxSupply { get; set; } = 1; // if you want strict NFTs set to 1; keep 1155 if needed
    public long TotalMinted { get; set; } = 0;
    public long TotalBurned { get; set; } = 0;
    public bool Transferable { get; set; } = true;

    public string? ObjectControllerAddress { get; set; } // who can change the object
    public bool ObjectFrozen { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
