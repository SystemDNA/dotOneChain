using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using dotOneChain.Api.Data;
using dotOneChain.Api.Models;
using dotOneChain.Api.Services;

namespace dotOneChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokensController : ControllerBase
{
    private readonly MongoContext _mongo;
    private readonly IContentStorage _storage;
    private readonly ICryptoService _crypto;
    private readonly IMempool _mempool;

    public TokensController(MongoContext mongo, IContentStorage storage, ICryptoService crypto, IMempool mempool)
    {
        _mongo = mongo; _storage = storage; _crypto = crypto; _mempool = mempool;
    }

    public record CreateTokenRequest(
        string? tokenId,
        string name,
        string description,
        long maxSupply,
        bool? transferable,
        string controllerPublicKeyPem,
        string objectJson // the NFT itself
    );

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTokenRequest req, CancellationToken ct)
    {
        string tokenId = string.IsNullOrWhiteSpace(req.tokenId) ? Guid.NewGuid().ToString("N") : req.tokenId!;
        string controllerAddr = _crypto.DeriveAddress(req.controllerPublicKeyPem);

        // Store Object JSON as v1 asset
        var canonical = JsonCanonicalizer.Canonicalize(req.objectJson);
        var sha = JsonCanonicalizer.Sha256Hex(canonical);
        string cid;
        using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(canonical)))
            cid = await _storage.AddFileAsync($"{tokenId}-v1.json", ms, ct);

        var tok = new Token1155
        {
            TokenId = tokenId,
            Name = req.name,
            Description = req.description,
            CurrentObjectCid = cid,
            CurrentVersion = 1,
            ObjectVersions = new List<AssetVersion>
            {
                new AssetVersion
                {
                    AssetCid = cid,
                    VersionNumber = 1,
                    PreviousAssetCid = string.Empty,
                    JsonSha256 = sha,
                    CommittedByAddress = controllerAddr,
                    SignatureBase64 = string.Empty,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            },
            MaxSupply = req.maxSupply,
            Transferable = req.transferable ?? true,
            ObjectControllerAddress = controllerAddr
        };

        await _mongo.Tokens.InsertOneAsync(tok, cancellationToken: ct);
        return Ok(new { tokenId, objectCid = cid, controllerAddress = controllerAddr, sha });
    }

    public record UpdateObjectRequest(
        string publicKeyPem,
        string signatureBase64,
        string newObjectJson,
        string previousObjectCid,
        int newVersion,
        long ts
    );

    [HttpPost("{tokenId}/object")]
    public async Task<IActionResult> UpdateObject([FromRoute] string tokenId, [FromBody] UpdateObjectRequest req, CancellationToken ct)
    {
        var canonical = JsonCanonicalizer.Canonicalize(req.newObjectJson);
        var sha = JsonCanonicalizer.Sha256Hex(canonical);
        string newCid;
        using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(canonical)))
            newCid = await _storage.AddFileAsync($"{tokenId}-v{req.newVersion}.json", ms, ct);

        var fromAddr = _crypto.DeriveAddress(req.publicKeyPem);
        var msg = _crypto.CanonicalUpdateObject(tokenId, newCid, req.previousObjectCid, req.newVersion, sha, req.ts);
        if (!_crypto.VerifySignature(req.publicKeyPem, msg, req.signatureBase64))
            return BadRequest("Invalid signature.");

        var tx = new Tx1155
        {
            Type = TxType.UpdateObject,
            TokenId = tokenId,
            FromAddress = fromAddr,
            PublicKeyPem = req.publicKeyPem,
            SignatureBase64 = req.signatureBase64,
            NewObjectCid = newCid,
            PreviousObjectCid = req.previousObjectCid,
            NewVersionNumber = req.newVersion,
            JsonSha256 = sha,
            TsMs = req.ts
        };

        _mempool.Enqueue(tx);
        return Accepted(new { queued = true, newCid, sha, txId = tx.Id });
    }

    [HttpPost("{tokenId}/freeze")]
    public async Task<IActionResult> FreezeObject([FromRoute] string tokenId, [FromBody] string controllerPublicKeyPem, CancellationToken ct)
    {
        var addr = _crypto.DeriveAddress(controllerPublicKeyPem);
        var res = await _mongo.Tokens.UpdateOneAsync(
            t => t.TokenId == tokenId && t.ObjectControllerAddress == addr && t.ObjectFrozen == false,
            Builders<Token1155>.Update.Set(t => t.ObjectFrozen, true),
            cancellationToken: ct);

        if (res.MatchedCount == 0) return Forbid("Not controller or already frozen.");
        return Ok(new { frozen = true });
    }

    [HttpGet("{tokenId}")]
    public async Task<IActionResult> Get(string tokenId, CancellationToken ct)
    {
        var tok = await _mongo.Tokens.Find(t => t.TokenId == tokenId).FirstOrDefaultAsync(ct);
        if (tok == null) return NotFound();
        var holdersCount = await _mongo.Holdings.CountDocumentsAsync(h => h.TokenId == tokenId && h.Balance > 0, cancellationToken: ct);
        var circulating = tok.TotalMinted - tok.TotalBurned;
        return Ok(new
        {
            tok.TokenId, tok.Name, tok.Description,
            CurrentObjectCid = tok.CurrentObjectCid,
            CurrentVersion = tok.CurrentVersion,
            tok.MaxSupply, tok.TotalMinted, tok.TotalBurned,
            Circulating = circulating,
            tok.Transferable,
            Holders = holdersCount,
            ObjectFrozen = tok.ObjectFrozen,
            ObjectController = tok.ObjectControllerAddress,
            Versions = tok.ObjectVersions
        });
    }

    [HttpGet("{tokenId}/versions")]
    public async Task<IActionResult> Versions(string tokenId, CancellationToken ct)
    {
        var tok = await _mongo.Tokens.Find(t => t.TokenId == tokenId)
                                     .Project(t => new { t.TokenId, t.CurrentVersion, t.CurrentObjectCid, t.ObjectVersions })
                                     .FirstOrDefaultAsync(ct);
        if (tok == null) return NotFound();
        return Ok(tok);
    }

    [HttpGet("{tokenId}/holders")]
    public async Task<IActionResult> Holders(string tokenId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var tokenExists = await _mongo.Tokens.Find(t => t.TokenId == tokenId).Project(t => t.TokenId).AnyAsync(ct);
        if (!tokenExists) return NotFound(new { message = "token not found", tokenId });

        var filter = Builders<Holding>.Filter.And(
            Builders<Holding>.Filter.Eq(h => h.TokenId, tokenId),
            Builders<Holding>.Filter.Gt(h => h.Balance, 0)
        );

        var total = await _mongo.Holdings.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await _mongo.Holdings.Find(filter)
            .SortByDescending(h => h.Balance)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project(h => new { h.OwnerAddress, h.Balance })
            .ToListAsync(ct);

        return Ok(new { tokenId, page, pageSize, total, items });
    }
}
