using dotOneChain.Api.Data;
using dotOneChain.Api.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<AuthorityOptions>(builder.Configuration.GetSection("Authority"));
builder.Services.Configure<IpfsOptions>(builder.Configuration.GetSection("Ipfs"));

builder.Services.AddSingleton<MongoContext>();

builder.Services.AddSingleton<IHashService, HashService>();
builder.Services.AddSingleton<IMerkleService, MerkleService>();
builder.Services.AddSingleton<ICryptoService, CryptoService>();
builder.Services.AddSingleton<IMempool, Mempool>();
builder.Services.AddSingleton<ILedgerService, LedgerService>();
builder.Services.AddHostedService<BlockProducer>();

builder.Services.AddPluggableStorage(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "dotOneChain API", Version = "v1" }));

var app = builder.Build();

var mongo = app.Services.GetRequiredService<MongoContext>();
await mongo.EnsureIndexesAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
