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
    public LedgerService(MongoContext mongo) { _mongo = mongo; }

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
            }
            await _mongo.Transactions.InsertOneAsync(tx, cancellationToken: ct);
        }
    }
}
