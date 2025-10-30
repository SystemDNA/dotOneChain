using DynamicFormsRazor.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DynamicFormsRazor.Services;

public class FormSubmissionRepository
{
    private readonly IMongoCollection<FormSubmission> _collection;

    public FormSubmissionRepository(MongoContext ctx)
    {
        _collection = ctx.Database.GetCollection<FormSubmission>("form_submissions");

        // Indexes for common filters
        var keys = Builders<FormSubmission>.IndexKeys
            .Ascending(s => s.FormKey)
            .Descending(s => s.FormVersion)
            .Descending(s => s.SubmittedAtUtc);
        _collection.Indexes.CreateOne(new CreateIndexModel<FormSubmission>(keys));
    }

    public async Task InsertAsync(FormSubmission sub)
        => await _collection.InsertOneAsync(sub);

    public async Task<List<FormSubmission>> ListByFormDefinitionAsync(string formDefinitionId)
        => await _collection.Find(s => s.FormDefinitionId == formDefinitionId)
            .SortByDescending(s => s.SubmittedAtUtc).ToListAsync();

    public async Task<List<FormSubmission>> ListByKeyAsync(string formKey, int? version = null, int limit = 200)
    {
        var filter = Builders<FormSubmission>.Filter.Eq(s => s.FormKey, formKey);
        if (version.HasValue)
            filter &= Builders<FormSubmission>.Filter.Eq(s => s.FormVersion, version.Value);

        return await _collection.Find(filter)
            .SortByDescending(s => s.SubmittedAtUtc)
            .Limit(limit)
            .ToListAsync();
    }
    public async Task<List<FormSubmission>> ListRecentAsync(int limit = 20)
    {
        return await _collection.Find(_ => true)
            .SortByDescending(s => s.SubmittedAtUtc)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, long>> CountByFormKeyAsync()
    {
        var pipeline = _collection.Aggregate()
            .Group(new BsonDocument
            {
            { "_id", "$FormKey" },
            { "count", new BsonDocument("$sum", 1) }
            });

        var docs = await pipeline.ToListAsync();

        var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in docs)
        {
            var key = d.GetValue("_id", BsonNull.Value).IsBsonNull ? "" : d["_id"].AsString;
            var cnt = d.GetValue("count", BsonInt64.Create(0)).ToInt64();
            if (!string.IsNullOrEmpty(key))
                dict[key] = cnt;
        }
        return dict;
    }

    private class FormKeyCount
    {
        public string Id { get; set; } = default!;
        public long Count { get; set; }
    }
}
