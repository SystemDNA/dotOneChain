using MongoDB.Bson.Serialization.Attributes;

namespace dotOneChain.Api.Models;

public class Block
{
    [BsonId] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long Index { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string PreviousHash { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string MerkleRoot { get; set; } = string.Empty;
    public string ProducerPublicKeyPem { get; set; } = string.Empty;
    public string ProducerSignatureBase64 { get; set; } = string.Empty;
    public int TxCount { get; set; }
    public string TransactionsJson { get; set; } = "[]";
}
