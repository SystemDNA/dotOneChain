using Microsoft.Extensions.Options;
using MongoDB.Driver;
using dotOneChain.Api.Data;
using MongoDB.Driver.GridFS;
using dotOneChain.Api.Models;


namespace dotOneChain.Api.Services;

public class StorageOptions
{
    public string Mode { get; set; } = "Mongo"; // or IPFS
}

public class IpfsOptions
{
    public string ApiUrl { get; set; } = "http://localhost:5001";
}

public interface IContentStorage
{
    Task<string> AddFileAsync(string fileName, Stream content, CancellationToken ct = default);
    Task<string> AddJsonAsync<T>(T value, CancellationToken ct = default);
    Task<(Stream? stream, string? fileName)> GetAsync(string cid, CancellationToken ct = default);
}

public class IpfsStorage : IContentStorage
{
    private readonly Ipfs.Http.IpfsClient _client;
    public IpfsStorage(IOptions<IpfsOptions> opts) => _client = new Ipfs.Http.IpfsClient(opts.Value.ApiUrl);

    public async Task<string> AddFileAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        var node = await _client.FileSystem.AddAsync(content, fileName, cancel: ct);
        return node.Id;
    }

    public async Task<string> AddJsonAsync<T>(T value, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value));
        var node = await _client.FileSystem.AddAsync(ms, "metadata.json", cancel: ct);
        return node.Id;
    }

    public Task<(Stream? stream, string? fileName)> GetAsync(string cid, CancellationToken ct = default)
        => Task.FromResult<(Stream?, string?)>((null, null)); // IPFS mode doesn't proxy content
}

// Mongo GridFS storage with content-addressed ids: "m" + sha256hex
public class MongoStorage : IContentStorage
{
    private readonly MongoContext _mongo;
    private readonly GridFSBucket _bucket;

    public MongoStorage(MongoContext mongo)
    {
        _mongo = mongo;
        _bucket = new GridFSBucket(mongo.Db);
    }

    public async Task<string> AddFileAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        byte[] sha;
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            sha = sha256.ComputeHash(content);
            content.Position = 0;
        }
        var cid = "m" + Convert.ToHexString(sha).ToLowerInvariant();

        var existing = await _mongo.Contents.Find(c => c.Cid == cid).FirstOrDefaultAsync(ct);
        if (existing != null) return cid;

        var oid = await _bucket.UploadFromStreamAsync(fileName, content, cancellationToken: ct);
        var rec = new StoredContent { Cid = cid, GridFsId = oid, FileName = fileName, Size = 0 };
        await _mongo.Contents.InsertOneAsync(rec, cancellationToken: ct);
        return cid;
    }

    public async Task<string> AddJsonAsync<T>(T value, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value));
        return await AddFileAsync("metadata.json", ms, ct);
    }

    public async Task<(Stream? stream, string? fileName)> GetAsync(string cid, CancellationToken ct = default)
    {
        var found = await _mongo.Contents.Find(c => c.Cid == cid).FirstOrDefaultAsync(ct);
        if (found == null) return (null, null);
        var ms = new MemoryStream();
        await _bucket.DownloadToStreamAsync(found.GridFsId, ms, cancellationToken: ct);
        ms.Position = 0;
        return (ms, found.FileName);
    }
}

public static class StorageRegistration
{
    public static void AddPluggableStorage(this IServiceCollection services, IConfiguration cfg)
    {
        var mode = cfg.GetSection("Storage")["Mode"] ?? "Mongo";
        if (string.Equals(mode, "IPFS", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IContentStorage, IpfsStorage>();
        else
            services.AddSingleton<IContentStorage, MongoStorage>();
    }
}
