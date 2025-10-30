namespace DynamicFormsRazor.Dtos
{
    // ------------------------------------------------------------------
    // Wallets
    // ------------------------------------------------------------------
    public record NewWalletResponse(
        string privateKeyPem,
        string publicKeyPem,
        string address
    );

    // ------------------------------------------------------------------
    // Token Creation (Object as NFT)
    // ------------------------------------------------------------------
    public record CreateTokenRequest(
        string? tokenId,
        string name,
        string description,
        long maxSupply,
        bool transferable,
        string controllerPublicKeyPem,
        string objectJson
    );

    public record CreateTokenResponse(
        string tokenId,
        string objectCid,
        string controllerAddress,
        string sha
    );

    // ------------------------------------------------------------------
    // Object Update / Freeze
    // ------------------------------------------------------------------
    public record UpdateObjectRequest(
        string publicKeyPem,
        string signatureBase64,
        string newObjectJson,
        string previousObjectCid,
        int newVersion,
        long ts
    );

    // ------------------------------------------------------------------
    // Supply Operations (Mint / Burn / Transfer)
    // ------------------------------------------------------------------
    public record MintRequest(
        string tokenId,
        string toAddress,
        long quantity
    );

    public record BurnRequest(
     string tokenId,
     string ownerAddress,
     long quantity,
     string publicKeyPem,
     string signatureBase64,
     long ts
 )
    {
        // Optional convenience overload
        public BurnRequest(string tokenId, string ownerAddress, long quantity)
            : this(tokenId, ownerAddress, quantity, string.Empty, string.Empty, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        { }
    }

    public record TransferRequest(
        string fromAddress,
        string toAddress,
        long quantity,
        string publicKeyPem,
        string signatureBase64,
        long ts
    );

    // ------------------------------------------------------------------
    // Enqueue / Transaction responses
    // ------------------------------------------------------------------
    public record EnqueueResponse(
        bool queued,
        string? newCid,
        string? sha,
        string? txId
    );

    public enum TxType
    {
        Mint = 1,
        Transfer = 2,
        Burn = 3,
        UpdateObject = 4
    }

    public record TxDto(
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

    // ------------------------------------------------------------------
    // Token / Portfolio
    // ------------------------------------------------------------------
    public record TokenDto(
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

    public record PortfolioItem(
        string tokenId,
        long balance,
        object? token
    );

    public record WalletPortfolio(
        string address,
        IEnumerable<PortfolioItem> items
    );

    // ------------------------------------------------------------------
    // Utility: CID calculation (Preflight)
    // ------------------------------------------------------------------
    public record CalcCidRequest(
        string objectJson,
        string? fileName,
        int? version
    );

    public record CalcCidResponse(
        string cid,
        string sha,
        string canonical
    );


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

        public string? NewObjectCid { get; set; }
        public int? NewVersionNumber { get; set; }
        public string? PreviousObjectCid { get; set; }
        public string? JsonSha256 { get; set; }
        public long? TsMs { get; set; }
    }
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
}
