using MongoDB.Bson.Serialization.Attributes;

namespace dotOneChain.Api.Models
{
    public class Wallet
    {
        [BsonId] public string Address { get; set; } = string.Empty; // use address as _id
        public string? PublicKeyPem { get; set; }                    // optional, can be null
        public string? Label { get; set; }                           // e.g., "Alice"
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? LastSeenAt { get; set; }              // update when used in tx
    }
}
