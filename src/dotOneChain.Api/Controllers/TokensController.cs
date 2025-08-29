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

    public TokensController(MongoContext mongo, IContentStorage storage) { _mongo = mongo; _storage = storage; }

    public record CreateTokenRequest(string? tokenId, string name, string description, long maxSupply, bool? transferable, string? metadataCid, IFormFile? mediaFile);

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Create([FromForm] CreateTokenRequest req, CancellationToken ct)
    {
        string tokenId = string.IsNullOrWhiteSpace(req.tokenId) ? Guid.NewGuid().ToString("N") : req.tokenId!;

        string? mediaCid = req.metadataCid;
        if (mediaCid == null && req.mediaFile != null)
        {
            await using var s = req.mediaFile.OpenReadStream();
            mediaCid = await _storage.AddFileAsync(req.mediaFile.FileName, s, ct);
        }

        var metadata = new { name = req.name, description = req.description, media = mediaCid };
        var metadataCid = await _storage.AddJsonAsync(metadata, ct);

        var tok = new Token1155 { TokenId = tokenId, Name = req.name, Description = req.description, MetadataCid = metadataCid, MaxSupply = req.maxSupply, Transferable = req.transferable ?? true };
        await _mongo.Tokens.InsertOneAsync(tok, cancellationToken: ct);
        return Ok(new { tokenId, metadataCid });
    }

    [HttpGet("{tokenId}")]
    public async Task<IActionResult> Get(string tokenId, CancellationToken ct)
    {
        var tok = await _mongo.Tokens.Find(t => t.TokenId == tokenId).FirstOrDefaultAsync(ct);
        if (tok == null) return NotFound();
        var holdersCount = await _mongo.Holdings.CountDocumentsAsync(h => h.TokenId == tokenId && h.Balance > 0, cancellationToken: ct);
        var circulating = tok.TotalMinted - tok.TotalBurned;
        return Ok(new { tok.TokenId, tok.Name, tok.Description, tok.MetadataCid, tok.MaxSupply, tok.TotalMinted, tok.TotalBurned, Circulating = circulating, tok.Transferable, Holders = holdersCount });
    }


    [HttpGet("{tokenId}/holders")]
    public async Task<IActionResult> Holders(
        string tokenId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            // normalize paging
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            // (optional) ensure the token exists; return 404 if not
            var tokenExists = await _mongo.Tokens
                .Find(t => t.TokenId == tokenId)
                .Project(t => t.TokenId)
                .AnyAsync(ct);
            if (!tokenExists)
                return NotFound(new { message = "token not found", tokenId });

            var filter = Builders<Holding>.Filter.And(
                Builders<Holding>.Filter.Eq(h => h.TokenId, tokenId),
                Builders<Holding>.Filter.Gt(h => h.Balance, 0)
            );

            // total count for pagination
            var total = await _mongo.Holdings.CountDocumentsAsync(filter, cancellationToken: ct);

            // project only what you need to send over the wire
            var projection = Builders<Holding>.Projection
                .Include(h => h.OwnerAddress)
                .Include(h => h.Balance);

            var sort = Builders<Holding>.Sort.Descending(h => h.Balance);

            var cursor = await _mongo.Holdings
                .Find(filter)
                .Project(projection)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(ct);

            // reshape BSON docs to a clean response (OwnerAddress, Balance)
            var items = cursor.Select(doc => new
            {
                OwnerAddress = doc["OwnerAddress"].AsString,
                Balance = doc["Balance"].ToInt64()
            });

            return Ok(new
            {
                tokenId,
                page,
                pageSize,
                total,
                items
            });
        }
        catch (MongoCommandException mce)
        {
            // bubble up with details so you can diagnose quickly
            return Problem(
                title: "Mongo command failed",
                detail: mce.Message,
                statusCode: 500);
        }
        catch (MongoException me)
        {
            return Problem(
                title: "Mongo error",
                detail: me.Message,
                statusCode: 500);
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Server error",
                detail: ex.Message,
                statusCode: 500);
        }
    }
}
