using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using dotOneChain.Api.Data;
using dotOneChain.Api.Models;

namespace dotOneChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChainController(MongoContext mongo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var blocks = await mongo.Blocks.Find(FilterDefinition<Block>.Empty).SortByDescending(b => b.Index).Limit(100).ToListAsync(ct);
        var count = await mongo.Blocks.CountDocumentsAsync(FilterDefinition<Block>.Empty, cancellationToken: ct);
        var txCount = await mongo.Transactions.CountDocumentsAsync(FilterDefinition<Tx1155>.Empty, cancellationToken: ct);
        return Ok(new { blocks, count, txCount });
    }
}
