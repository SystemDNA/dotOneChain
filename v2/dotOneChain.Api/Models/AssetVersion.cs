namespace dotOneChain.Api.Models;

public class AssetVersion
{
    public string AssetCid { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string? PreviousAssetCid { get; set; }
    public string JsonSha256 { get; set; } = string.Empty;
    public string CommittedByAddress { get; set; } = string.Empty;
    public string SignatureBase64 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
