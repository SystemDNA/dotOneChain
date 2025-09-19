using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using dotOneChain.Api.Data;
using dotOneChain.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace dotOneChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly MongoContext _mongo;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(MongoContext mongo, ILogger<TransactionsController> logger)
    {
        _mongo = mongo;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById([FromRoute, Required] string id, CancellationToken ct = default)
    {
        var tx = await _mongo.Transactions.Find(t => t.Id == id).FirstOrDefaultAsync(ct);
        return tx is null ? NotFound() : Ok(tx);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? owner,
        [FromQuery] string? tokenId,
        [FromQuery] string? type,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var f = Builders<Tx1155>.Filter;
        var filters = new List<FilterDefinition<Tx1155>>();

        if (!string.IsNullOrWhiteSpace(owner))
            filters.Add(f.Or(f.Eq(t => t.FromAddress, owner), f.Eq(t => t.ToAddress, owner)));

        if (!string.IsNullOrWhiteSpace(tokenId))
            filters.Add(f.Eq(t => t.TokenId, tokenId));

        if (!string.IsNullOrWhiteSpace(type))
        {
            var types = ParseTypes(type);
            if (types.Length > 0) filters.Add(f.In(t => t.Type, types));
        }

        if (from.HasValue) filters.Add(f.Gte(t => t.CreatedAt, from.Value));
        if (to.HasValue) filters.Add(f.Lte(t => t.CreatedAt, to.Value));

        var filter = filters.Count switch { 0 => f.Empty, 1 => filters[0], _ => f.And(filters) };

        var find = _mongo.Transactions.Find(filter);
        var total = await find.CountDocumentsAsync(ct);

        var items = await find.SortByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? type, [FromQuery] int? max, CancellationToken ct = default)
    {
        var f = Builders<Tx1155>.Filter;
        var filter = f.Empty;

        if (!string.IsNullOrWhiteSpace(type))
        {
            var types = ParseTypes(type);
            if (types.Length > 0) filter = f.In(t => t.Type, types);
        }

        var sort = Builders<Tx1155>.Sort.Descending(t => t.CreatedAt);
        var opts = new FindOptions<Tx1155> { Sort = sort, Limit = (max.HasValue && max.Value > 0) ? Math.Min(max.Value, 100_000) : (int?)null };
        using var cursor = await _mongo.Transactions.FindAsync(filter, opts, ct);
        var items = await cursor.ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetTypes(CancellationToken ct = default)
    {
        Expression<Func<Tx1155, TxType>> field = t => t.Type;
        var values = await _mongo.Transactions.Distinct(field, FilterDefinition<Tx1155>.Empty).ToListAsync(ct);
        var result = values.Distinct().OrderBy(v => (int)v).Select(v => new { value = (int)v, name = v.ToString() }).ToList();
        return Ok(result);
    }

    private static TxType[] ParseTypes(string typeParam)
    {
        var list = new List<TxType>();
        foreach (var raw in typeParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(raw, out var intVal) && Enum.IsDefined(typeof(TxType), intVal))
                list.Add((TxType)intVal);
            else if (Enum.TryParse<TxType>(raw, true, out var enumVal))
                list.Add(enumVal);
        }
        return list.Distinct().ToArray();
    }
}
