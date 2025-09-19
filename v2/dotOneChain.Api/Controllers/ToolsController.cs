using Microsoft.AspNetCore.Mvc;
using dotOneChain.Api.Services;
using System.Text;

namespace dotOneChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolsController(IContentStorage storage) : ControllerBase
{
    public record CalcCidRequest(string objectJson, string? fileName, int? version);
    public record CalcCidResponse(string cid, string sha, string canonical);

    /// <summary>
    /// Preflight: canonicalize given JSON, store it, and return CID + SHA + canonical string.
    /// This lets clients sign the correct canonical string including the final CID.
    /// Idempotent: storage is content-addressed; re-upload yields same CID without duplication.
    /// </summary>
    [HttpPost("calc-cid")]
    public async Task<IActionResult> CalcCid([FromBody] CalcCidRequest req, CancellationToken ct)
    {
        var canonical = JsonCanonicalizer.Canonicalize(req.objectJson);
        var sha = JsonCanonicalizer.Sha256Hex(canonical);
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(canonical));
        var name = string.IsNullOrWhiteSpace(req.fileName) ? $"object-v{req.version ?? 0}.json" : req.fileName!;
        var cid = await storage.AddFileAsync(name, ms, ct);
        return Ok(new CalcCidResponse(cid, sha, canonical));
    }
}
