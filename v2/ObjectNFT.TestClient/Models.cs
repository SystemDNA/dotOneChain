using System.Text.Json.Serialization;

namespace ObjectNFT.TestClient;

public record NewWalletResponse(string privateKeyPem, string publicKeyPem, string address);

public record CreateTokenRequest(
    string? tokenId,
    string name,
    string description,
    long maxSupply,
    bool? transferable,
    string controllerPublicKeyPem,
    string objectJson
);

public record CreateTokenResponse(string tokenId, string objectCid, string controllerAddress, string sha);

public record UpdateObjectRequest(
    string publicKeyPem,
    string signatureBase64,
    string newObjectJson,
    string previousObjectCid,
    int newVersion,
    long ts
);

public record EnqueueResponse(bool queued, string? newCid, string? sha, string? txId);

public record TokenSummary(
    string TokenId,
    string Name,
    string Description,
    string CurrentObjectCid,
    int CurrentVersion,
    long MaxSupply,
    long TotalMinted,
    long TotalBurned,
    long Circulating,
    bool Transferable,
    long Holders,
    bool ObjectFrozen,
    string? ObjectController
);

public record MintRequest(string tokenId, string toAddress, long quantity);
public record TransferRequest(string fromAddress, string toAddress, long quantity, string publicKeyPem, string signatureBase64, long ts);
public record BurnRequest(string ownerAddress, long quantity, string publicKeyPem, string signatureBase64, long ts);

public enum TxType { Mint = 1, Transfer = 2, Burn = 3, UpdateObject = 4 }

public record Tx1155(
    string Id,
    TxType Type,
    string TokenId,
    string FromAddress,
    string ToAddress,
    long Quantity,
    string PublicKeyPem,
    string SignatureBase64,
    DateTimeOffset CreatedAt,
    string? NewObjectCid,
    int? NewVersionNumber,
    string? PreviousObjectCid,
    string? JsonSha256,
    long? TsMs
);

public record PortfolioItem(string tokenId, long balance, object? token);
public record WalletPortfolio(string address, IEnumerable<PortfolioItem> items);

public record CalcCidRequest(string objectJson, string? fileName, int? version);
public record CalcCidResponse(string cid, string sha, string canonical);
