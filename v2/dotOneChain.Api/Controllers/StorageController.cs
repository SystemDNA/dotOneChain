using Microsoft.AspNetCore.Mvc;
using dotOneChain.Api.Services;

namespace dotOneChain.Api.Controllers;

public record UploadFileRequest(IFormFile File);

[ApiController]
[Route("api/[controller]")]
public class StorageController(IContentStorage storage) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Upload([FromForm] UploadFileRequest req, CancellationToken ct)
    {
        if (req.File is null || req.File.Length == 0) return BadRequest("No file provided.");
        await using var s = req.File.OpenReadStream();
        var cid = await storage.AddFileAsync(req.File.FileName, s, ct);
        return Ok(new { cid });
    }

    [HttpGet("{cid}")]
    public async Task<IActionResult> Get(string cid, CancellationToken ct)
    {
        var (stream, fileName) = await storage.GetAsync(cid, ct);
        if (stream == null) return NotFound();
        return File(stream, "application/octet-stream", fileName ?? "file.bin");
    }
}
