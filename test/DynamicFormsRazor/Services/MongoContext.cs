using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DynamicFormsRazor.Services;

public class MongoContext
{
    public IMongoDatabase Database { get; }

    public MongoContext(IConfiguration config)
    {
        var conn = config["Mongo:ConnectionString"] ?? "mongodb://localhost:27017";
        var dbName = config["Mongo:Database"] ?? "DynamicFormsDb";
        var client = new MongoClient(conn);
        Database = client.GetDatabase(dbName);
    }
}
