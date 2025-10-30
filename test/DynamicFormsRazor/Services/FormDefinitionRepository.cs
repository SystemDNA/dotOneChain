using DynamicFormsRazor.Models;
using MongoDB.Driver;

namespace DynamicFormsRazor.Services;

public class FormDefinitionRepository
{
    private readonly IMongoCollection<FormDefinition> _collection;

    public FormDefinitionRepository(MongoContext ctx)
    {
        _collection = ctx.Database.GetCollection<FormDefinition>("form_definitions");
        // Helpful index for versioning queries
        var keys = Builders<FormDefinition>.IndexKeys
            .Ascending(x => x.FormKey)
            .Descending(x => x.Version)
            .Ascending(x => x.IsCurrent);
        _collection.Indexes.CreateOne(new CreateIndexModel<FormDefinition>(keys));
    }

    /// <summary>List only current versions across all form keys.</summary>
    public async Task<List<FormDefinition>> ListCurrentsAsync()
        => await _collection.Find(f => f.IsCurrent).SortBy(f => f.FormKey).ToListAsync();

    /// <summary>Get the current version document by form key.</summary>
    public async Task<FormDefinition?> GetCurrentByKeyAsync(string formKey)
        => await _collection.Find(f => f.FormKey == formKey && f.IsCurrent).FirstOrDefaultAsync();

    /// <summary>Get any version by Id.</summary>
    public async Task<FormDefinition?> GetAsync(string id)
        => await _collection.Find(f => f.Id == id).FirstOrDefaultAsync();

    /// <summary>List all versions (newest first) for a form key.</summary>
    public async Task<List<FormDefinition>> ListVersionsAsync(string formKey)
        => await _collection.Find(f => f.FormKey == formKey)
            .SortByDescending(f => f.Version).ToListAsync();

    /// <summary>
    /// Publish a new version for FormKey. Marks previous current (if any) as not current,
    /// increments Version, and inserts new doc as current.
    /// </summary>
    public async Task InsertNewVersionAsync(FormDefinition newDef)
    {
        if (string.IsNullOrWhiteSpace(newDef.FormKey))
            throw new ArgumentException("FormKey is required");

        // get latest version number
        var latest = await _collection.Find(f => f.FormKey == newDef.FormKey)
            .SortByDescending(f => f.Version)
            .Limit(1)
            .FirstOrDefaultAsync();

        var nextVersion = (latest?.Version ?? 0) + 1;

        // unset previous current
        var unsetCurrents = Builders<FormDefinition>.Update.Set(f => f.IsCurrent, false);
        await _collection.UpdateManyAsync(f => f.FormKey == newDef.FormKey && f.IsCurrent, unsetCurrents);

        // insert new current
        newDef.Version = nextVersion;
        newDef.IsCurrent = true;
        newDef.CreatedAtUtc = DateTime.UtcNow;

        await _collection.InsertOneAsync(newDef);
    }


}
