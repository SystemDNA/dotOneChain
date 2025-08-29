namespace dotOneChain.Api.Models;

public enum TxType { Mint = 1, Transfer = 2, Burn = 3 }

public class Tx1155
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TxType Type { get; set; }
    public string TokenId { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public long Quantity { get; set; } = 0;
    public string PublicKeyPem { get; set; } = string.Empty;
    public string SignatureBase64 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
