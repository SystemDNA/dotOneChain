using MongoDB.Bson.Serialization.Attributes;

namespace dotOneChain.Api.Models
{
    public class Wallet
    {
        [BsonId] public string Address { get; set; } = string.Empty;
        public string? PublicKeyPem { get; set; }
        public string? Label { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? LastSeenAt { get; set; }
    }
}
