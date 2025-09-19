using MongoDB.Driver;
using dotOneChain.Api.Data;
using dotOneChain.Api.Models;

namespace dotOneChain.Api.Services;

public interface ILedgerService
{
    Task<bool> ValidateAsync(Tx1155 tx, CancellationToken ct = default);
    Task ApplyAsync(IEnumerable<Tx1155> txs, CancellationToken ct = default);
}

public class LedgerService : ILedgerService
{
    private readonly MongoContext _mongo;
    private readonly ICryptoService _crypto;

    public LedgerService(MongoContext mongo, ICryptoService crypto)
    {
        _mongo = mongo;
        _crypto = crypto;
    }

    public async Task<bool> ValidateAsync(Tx1155 tx, CancellationToken ct = default)
    {
        var token = await _mongo.Tokens.Find(t => t.TokenId == tx.TokenId).FirstOrDefaultAsync(ct);
        if (token is null) return false;

        switch (tx.Type)
        {
            case TxType.Mint:
                if (tx.Quantity <= 0) return false;
                if (token.MaxSupply > 0 && token.TotalMinted + tx.Quantity > token.MaxSupply) return false;
                return true;

            case TxType.Transfer:
                if (!token.Transferable) return false;
                if (tx.Quantity <= 0) return false;
                var from = await _mongo.Holdings.Find(h => h.TokenId == tx.TokenId && h.OwnerAddress == tx.FromAddress).FirstOrDefaultAsync(ct);
                return from != null && from.Balance >= tx.Quantity;

            case TxType.Burn:
                if (tx.Quantity <= 0) return false;
                var owner = await _mongo.Holdings.Find(h => h.TokenId == tx.TokenId && h.OwnerAddress == tx.FromAddress).FirstOrDefaultAsync(ct);
                return owner != null && owner.Balance >= tx.Quantity;

            case TxType.UpdateObject:
                if (token.ObjectFrozen) return false;
                if (string.IsNullOrWhiteSpace(tx.NewObjectCid)) return false;
                if (string.IsNullOrWhiteSpace(tx.PreviousObjectCid)) return false;
                if (!tx.NewVersionNumber.HasValue || tx.NewVersionNumber.Value <= 0) return false;
                if (string.IsNullOrWhiteSpace(tx.PublicKeyPem) || string.IsNullOrWhiteSpace(tx.SignatureBase64)) return false;
                if (string.IsNullOrWhiteSpace(tx.JsonSha256)) return false;
                if (!tx.TsMs.HasValue) return false;

                if (string.IsNullOrWhiteSpace(token.ObjectControllerAddress)) return false;

                var signerAddr = _crypto.DeriveAddress(tx.PublicKeyPem);
                if (!string.Equals(signerAddr, token.ObjectControllerAddress, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (token.CurrentVersion == 0)
                {
                    if (tx.NewVersionNumber.Value != 1) return false;
                    if (!string.Equals(token.CurrentObjectCid, tx.PreviousObjectCid, StringComparison.Ordinal)) return false;
                }
                else
                {
                    if (tx.NewVersionNumber.Value != token.CurrentVersion + 1) return false;
                    if (!string.Equals(token.CurrentObjectCid, tx.PreviousObjectCid, StringComparison.Ordinal)) return false;
                }

                var msg = _crypto.CanonicalUpdateObject(token.TokenId, tx.NewObjectCid!, tx.PreviousObjectCid!, tx.NewVersionNumber!.Value, tx.JsonSha256!, tx.TsMs!.Value);
                if (!_crypto.VerifySignature(tx.PublicKeyPem, msg, tx.SignatureBase64)) return false;

                return true;

            default: return false;
        }
    }

    public async Task ApplyAsync(IEnumerable<Tx1155> txs, CancellationToken ct = default)
    {
        foreach (var tx in txs)
        {
            switch (tx.Type)
            {
                case TxType.Mint:
                    await _mongo.Holdings.UpdateOneAsync(
                        h => h.TokenId == tx.TokenId && h.OwnerAddress == tx.ToAddress,
                        Builders<Holding>.Update.Inc(h => h.Balance, tx.Quantity),
                        new UpdateOptions { IsUpsert = true }, ct);
                    await _mongo.Tokens.UpdateOneAsync(t => t.TokenId == tx.TokenId,
                        Builders<Token1155>.Update.Inc(t => t.TotalMinted, tx.Quantity), cancellationToken: ct);
                    break;

                case TxType.Transfer:
                    await _mongo.Holdings.UpdateOneAsync(
                        h => h.TokenId == tx.TokenId && h.OwnerAddress == tx.FromAddress,
                        Builders<Holding>.Update.Inc(h => h.Balance, -tx.Quantity), cancellationToken: ct);
                    await _mongo.Holdings.UpdateOneAsync(
                        h => h.TokenId == tx.TokenId && h.OwnerAddress == tx.ToAddress,
                        Builders<Holding>.Update.Inc(h => h.Balance, tx.Quantity),
                        new UpdateOptions { IsUpsert = true }, ct);
                    break;

                case TxType.Burn:
                    await _mongo.Holdings.UpdateOneAsync(
                        h => h.TokenId == tx.TokenId && h.OwnerAddress == tx.FromAddress,
                        Builders<Holding>.Update.Inc(h => h.Balance, -tx.Quantity), cancellationToken: ct);
                    await _mongo.Tokens.UpdateOneAsync(t => t.TokenId == tx.TokenId,
                        Builders<Token1155>.Update.Inc(t => t.TotalBurned, tx.Quantity), cancellationToken: ct);
                    break;

                case TxType.UpdateObject:
                    var update = Builders<Token1155>.Update
                        .Push(t => t.ObjectVersions, new AssetVersion
                        {
                            AssetCid = tx.NewObjectCid!,
                            PreviousAssetCid = tx.PreviousObjectCid!,
                            VersionNumber = tx.NewVersionNumber!.Value,
                            JsonSha256 = tx.JsonSha256!,
                            CommittedByAddress = tx.FromAddress,
                            SignatureBase64 = tx.SignatureBase64,
                            CreatedAt = tx.CreatedAt
                        })
                        .Set(t => t.CurrentObjectCid, tx.NewObjectCid!)
                        .Set(t => t.CurrentVersion, tx.NewVersionNumber!.Value);

                    var filter = Builders<Token1155>.Filter.And(
                        Builders<Token1155>.Filter.Eq(t => t.TokenId, tx.TokenId),
                        Builders<Token1155>.Filter.Eq(t => t.ObjectControllerAddress, _crypto.DeriveAddress(tx.PublicKeyPem)),
                        Builders<Token1155>.Filter.Eq(t => t.CurrentObjectCid, tx.PreviousObjectCid!)
                    );

                    var res = await _mongo.Tokens.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<Token1155>
                    {
                        IsUpsert = false, ReturnDocument = ReturnDocument.After
                    }, ct);

                    if (res is null)
                        throw new InvalidOperationException("Concurrent update or precondition failed for object update.");
                    break;
            }
            await _mongo.Transactions.InsertOneAsync(tx, cancellationToken: ct);
        }
    }
}
