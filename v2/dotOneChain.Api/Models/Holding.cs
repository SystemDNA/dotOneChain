using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotOneChain.Api.Models;

[BsonIgnoreExtraElements]
public class Holding
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();
    public string TokenId { get; set; } = string.Empty;
    public string OwnerAddress { get; set; } = string.Empty;
    public long Balance { get; set; } = 0;
}
