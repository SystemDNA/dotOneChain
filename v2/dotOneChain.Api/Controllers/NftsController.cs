using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using dotOneChain.Api.Data;
using dotOneChain.Api.Models;
using dotOneChain.Api.Services;

namespace dotOneChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NftsController : ControllerBase
{
    private readonly MongoContext _mongo;
    private readonly IMempool _mempool;
    private readonly ICryptoService _crypto;

    public NftsController(MongoContext mongo, IMempool mempool, ICryptoService crypto) { _mongo = mongo; _mempool = mempool; _crypto = crypto; }

    public record MintRequest(string tokenId, string toAddress, long quantity);

    [HttpPost("mint")]
    public async Task<IActionResult> Mint([FromBody] MintRequest req, CancellationToken ct)
    {
        if (req.quantity <= 0) return BadRequest("quantity must be > 0");
        var tok = await _mongo.Tokens.Find(t => t.TokenId == req.tokenId).FirstOrDefaultAsync(ct);
        if (tok == null) return NotFound("token not found");

        var tx = new Tx1155 { Type = TxType.Mint, TokenId = req.tokenId, FromAddress = "MINT", ToAddress = req.toAddress, Quantity = req.quantity };
        _mempool.Enqueue(tx);
        return Accepted(new { queued = true, txId = tx.Id });
    }

    public record TransferRequest(string fromAddress, string toAddress, long quantity, string publicKeyPem, string signatureBase64, long ts);

    [HttpPost("{tokenId}/transfer")]
    public async Task<IActionResult> Transfer([FromRoute] string tokenId, [FromBody] TransferRequest req)
    {
        if (req.quantity <= 0) return BadRequest("quantity must be > 0");
        var msg = _crypto.CanonicalTransfer(tokenId, req.fromAddress, req.toAddress, req.quantity, req.ts);
        if (!_crypto.VerifySignature(req.publicKeyPem, msg, req.signatureBase64)) return BadRequest("Invalid signature.");
        var tx = new Tx1155 { Type = TxType.Transfer, TokenId = tokenId, FromAddress = req.fromAddress, ToAddress = req.toAddress, Quantity = req.quantity, PublicKeyPem = req.publicKeyPem, SignatureBase64 = req.signatureBase64 };
        _mempool.Enqueue(tx);
        return Accepted(new { queued = true, txId = tx.Id });
    }

    public record BurnRequest(string ownerAddress, long quantity, string publicKeyPem, string signatureBase64, long ts);

    [HttpPost("{tokenId}/burn")]
    public async Task<IActionResult> Burn([FromRoute] string tokenId, [FromBody] BurnRequest req)
    {
        if (req.quantity <= 0) return BadRequest("quantity must be > 0");
        var msg = _crypto.CanonicalBurn(tokenId, req.ownerAddress, req.quantity, req.ts);
        if (!_crypto.VerifySignature(req.publicKeyPem, msg, req.signatureBase64)) return BadRequest("Invalid signature.");
        var tx = new Tx1155 { Type = TxType.Burn, TokenId = tokenId, FromAddress = req.ownerAddress, ToAddress = "BURN", Quantity = req.quantity, PublicKeyPem = req.publicKeyPem, SignatureBase64 = req.signatureBase64 };
        _mempool.Enqueue(tx);
        return Accepted(new { queued = true, txId = tx.Id });
    }
}
