using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using dotOneChain.Api.Services;
using dotOneChain.Api.Models;
using MongoDB.Driver;
using dotOneChain.Api.Data;

namespace dotOneChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletsController(ICryptoService crypto,MongoContext mongo) : ControllerBase
{
    [HttpPost("new")]
    public async Task<IActionResult> NewWallet(CancellationToken ct)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePem = ecdsa.ExportPkcs8PrivateKeyPem();
        var publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var address = crypto.DeriveAddress(publicPem);

        // save (without private key!)
        var wallet = new Wallet { Address = address, PublicKeyPem = publicPem };
        await mongo.Wallets.ReplaceOneAsync(w => w.Address == address, wallet,
            new ReplaceOptions { IsUpsert = true }, ct);

        return Ok(new { privateKeyPem = privatePem, publicKeyPem = publicPem, address });
    }

    [HttpGet("{address}")]
    public async Task<IActionResult> GetWallet(string address, CancellationToken ct)
    {
        var w = await mongo.Wallets.Find(x => x.Address == address).FirstOrDefaultAsync(ct);
        return Ok(w);
    }

    [HttpGet("{address}/portfolio")]
    public async Task<IActionResult> Portfolio(string address, CancellationToken ct)
    {
        var holdings = await mongo.Holdings
            .Find(h => h.OwnerAddress == address && h.Balance > 0)
            .ToListAsync(ct);

        // join token metadata
        var tokenIds = holdings.Select(h => h.TokenId).ToList();
        var tokens = await mongo.Tokens.Find(t => tokenIds.Contains(t.TokenId)).ToListAsync(ct);
        var byId = tokens.ToDictionary(t => t.TokenId);

        var items = holdings.Select(h => new {
            tokenId = h.TokenId,
            balance = h.Balance,
            token = byId.TryGetValue(h.TokenId, out var t) ? new
            {
                t.Name,
                t.Description,
                t.MetadataCid,
                t.MaxSupply,
                Circulating = t.TotalMinted - t.TotalBurned
            } : null
        });

        return Ok(new { address, items });
    }

    [HttpGet("{address}/history")]
    public async Task<IActionResult> History(string address, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);

        // Add helpful indexes first (see next snippet)
        var filter = Builders<Tx1155>.Filter.Or(
            Builders<Tx1155>.Filter.Eq(t => t.FromAddress, address),
            Builders<Tx1155>.Filter.Eq(t => t.ToAddress, address));

        var list = await mongo.Transactions.Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return Ok(new { address, page, pageSize, items = list });
    }
}
