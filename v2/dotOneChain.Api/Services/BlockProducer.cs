using System.Text.Json;
using dotOneChain.Api.Data;
using dotOneChain.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace dotOneChain.Api.Services;

public class AuthorityOptions
{
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
}

public class BlockProducer : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMempool _mempool;
    private readonly IHashService _hash;
    private readonly IMerkleService _merkle;
    private readonly ICryptoService _crypto;
    private readonly AuthorityOptions _auth;

    public BlockProducer(IServiceScopeFactory scopeFactory, IMempool mempool, IHashService hash, IMerkleService merkle, ICryptoService crypto, IOptions<AuthorityOptions> auth)
    {
        _scopeFactory = scopeFactory; _mempool = mempool; _hash = hash; _merkle = merkle; _crypto = crypto; _auth = auth.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProduceAsync(stoppingToken); } catch (Exception ex) { Console.WriteLine($"[BlockProducer] {ex.Message}"); }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProduceAsync(CancellationToken ct)
    {
        var drained = _mempool.Drain(500);
        if (drained.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();

        var mints = drained.Where(t => t.Type == TxType.Mint).ToList();
        var transfers = drained.Where(t => t.Type == TxType.Transfer).ToList();
        var burns = drained.Where(t => t.Type == TxType.Burn).ToList();
        var objUpdates = drained.Where(t => t.Type == TxType.UpdateObject).ToList();

        async Task<List<Tx1155>> ValidateApplyAsync(List<Tx1155> batch)
        {
            var keep = new List<Tx1155>();
            foreach (var tx in batch)
                if (await ledger.ValidateAsync(tx, ct)) keep.Add(tx);
            if (keep.Count > 0)
                await ledger.ApplyAsync(keep, ct);
            return keep;
        }

        var appliedMints = await ValidateApplyAsync(mints);
        var appliedTransfers = await ValidateApplyAsync(transfers);
        var appliedBurns = await ValidateApplyAsync(burns);
        var appliedObj = await ValidateApplyAsync(objUpdates);

        var appliedAll = appliedMints.Count + appliedTransfers.Count + appliedBurns.Count + appliedObj.Count;
        if (appliedAll == 0) return;

        var applied = appliedMints.Concat(appliedTransfers).Concat(appliedBurns).Concat(appliedObj).ToList();

        var prev = await mongo.Blocks.Find(FilterDefinition<Block>.Empty).SortByDescending(b => b.Index).FirstOrDefaultAsync(ct);
        var index = (prev?.Index ?? 0) + 1;
        var prevHash = prev?.Hash ?? string.Empty;

        var txJson = JsonSerializer.Serialize(applied);
        var merkle = _merkle.ComputeMerkleRoot(applied.Select(t => t.Id));
        var payload = $"{index}|{prevHash}|{merkle}|{txJson}";
        var hash = _hash.Sha256Hex(payload);

        var signature = string.Empty;
        var publicKey = string.Empty;
        if (!string.IsNullOrWhiteSpace(_auth.PrivateKeyPem))
        {
            signature = _crypto.SignWithPrivateKey(_auth.PrivateKeyPem, hash);
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();
            ecdsa.ImportFromPem(_auth.PrivateKeyPem);
            publicKey = ecdsa.ExportSubjectPublicKeyInfoPem();
        }

        var block = new Block
        {
            Index = index,
            PreviousHash = prevHash,
            Hash = hash,
            MerkleRoot = merkle,
            TransactionsJson = txJson,
            ProducerSignatureBase64 = signature,
            ProducerPublicKeyPem = publicKey,
            TxCount = applied.Count,
            Timestamp = DateTimeOffset.UtcNow
        };
        await mongo.Blocks.InsertOneAsync(block, cancellationToken: ct);
    }
}
