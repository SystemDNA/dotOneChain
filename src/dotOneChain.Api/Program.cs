using Microsoft.OpenApi.Models;
using dotOneChain.Api.Data;
using dotOneChain.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.Configure<MongoOptions>(cfg.GetSection("Mongo"));
builder.Services.Configure<IpfsOptions>(cfg.GetSection("IPFS"));
builder.Services.Configure<StorageOptions>(cfg.GetSection("Storage"));
builder.Services.Configure<AuthorityOptions>(cfg.GetSection("Authority"));

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<IHashService, HashService>();
builder.Services.AddSingleton<IMerkleService, MerkleService>();
builder.Services.AddSingleton<IMempool, Mempool>();
builder.Services.AddSingleton<ICryptoService, CryptoService>();
builder.Services.AddPluggableStorage(cfg);
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddHostedService<BlockProducer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "dotOneChain API", Version = "v1" }); });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
    await mongo.EnsureIndexesAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
