using Microsoft.Extensions.Options;
using MongoDB.Driver;
using dotOneChain.Api.Models;

namespace dotOneChain.Api.Data;

public class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "nftchain";
}

public class MongoContext
{
    public IMongoDatabase Db { get; }
    public IMongoCollection<Block> Blocks => Db.GetCollection<Block>("blocks");
    public IMongoCollection<Token1155> Tokens => Db.GetCollection<Token1155>("tokens");
    public IMongoCollection<Holding> Holdings => Db.GetCollection<Holding>("holdings");
    public IMongoCollection<Tx1155> Transactions => Db.GetCollection<Tx1155>("transactions");
    public IMongoCollection<StoredContent> Contents => Db.GetCollection<StoredContent>("contents");
    public IMongoCollection<Wallet> Wallets => Db.GetCollection<Wallet>("wallets");

    public MongoContext(IOptions<MongoOptions> opts)
    {
        var client = new MongoClient(opts.Value.ConnectionString);
        Db = client.GetDatabase(opts.Value.Database);
    }

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await Blocks.Indexes.CreateOneAsync(
            new CreateIndexModel<Block>(Builders<Block>.IndexKeys.Ascending(b => b.Index),
                new CreateIndexOptions { Unique = true, Name = "idx_blocks_index" }), cancellationToken: ct);

        await Tokens.Indexes.CreateManyAsync(new[]{
            new CreateIndexModel<Token1155>(
                Builders<Token1155>.IndexKeys.Ascending(t => t.TokenId),
                new CreateIndexOptions { Unique = true, Name = "idx_tokens_tokenId" }),
            new CreateIndexModel<Token1155>(
                Builders<Token1155>.IndexKeys.Ascending(t => t.TokenId).Ascending(t => t.CurrentVersion),
                new CreateIndexOptions { Name = "idx_tokens_tokenId_ver" })
        }, ct);

        await Holdings.Indexes.CreateOneAsync(
            new CreateIndexModel<Holding>(Builders<Holding>.IndexKeys.Ascending(h => h.TokenId).Ascending(h => h.OwnerAddress),
                new CreateIndexOptions { Unique = true, Name = "idx_holdings_token_owner" }), cancellationToken: ct);

        await Holdings.Indexes.CreateOneAsync(
            new CreateIndexModel<Holding>(Builders<Holding>.IndexKeys.Ascending(h => h.OwnerAddress),
                new CreateIndexOptions { Name = "idx_holdings_owner" }), cancellationToken: ct);

        await Transactions.Indexes.CreateManyAsync(new[] {
            new CreateIndexModel<Tx1155>(Builders<Tx1155>.IndexKeys.Ascending(t => t.TokenId), new CreateIndexOptions { Name = "idx_tx_token" }),
            new CreateIndexModel<Tx1155>(Builders<Tx1155>.IndexKeys.Ascending(t => t.Type), new CreateIndexOptions { Name = "idx_tx_type" }),
            new CreateIndexModel<Tx1155>(Builders<Tx1155>.IndexKeys.Ascending(t => t.Type).Ascending(t => t.TokenId).Ascending(t => t.CreatedAt),
                new CreateIndexOptions { Name = "idx_tx_type_token_time" }),
            new CreateIndexModel<Tx1155>(Builders<Tx1155>.IndexKeys.Ascending(t => t.FromAddress).Ascending(t => t.CreatedAt),
                new CreateIndexOptions { Name = "idx_tx_from_time" }),
            new CreateIndexModel<Tx1155>(Builders<Tx1155>.IndexKeys.Ascending(t => t.ToAddress).Ascending(t => t.CreatedAt),
                new CreateIndexOptions { Name = "idx_tx_to_time" })
        }, ct);

        await Contents.Indexes.CreateOneAsync(
            new CreateIndexModel<StoredContent>(Builders<StoredContent>.IndexKeys.Ascending(c => c.Cid),
                new CreateIndexOptions { Unique = true, Name = "idx_contents_cid" }), cancellationToken: ct);

        await Wallets.Indexes.CreateOneAsync(
            new CreateIndexModel<Wallet>(Builders<Wallet>.IndexKeys.Ascending(w => w.Label),
                new CreateIndexOptions { Name = "idx_wallets_label" }), cancellationToken: ct);
    }
}
