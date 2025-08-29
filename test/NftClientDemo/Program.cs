using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

static class Api
{
    public const string BaseUrl = "http://localhost:8080";

    // dotOneChain endpoints (MATCH the API)
    public const string WalletNew = "/api/wallets/new";                     // GET
    public const string TokenCreate = "/api/tokens";                         // POST multipart/form-data
    public const string NftMint = "/api/nfts/mint";                          // POST application/json
    public static string NftTransfer(string tokenId) => $"/api/nfts/{tokenId}/transfer"; // POST application/json
    public static string NftBurn(string tokenId) => $"/api/nfts/{tokenId}/burn";         // POST application/json
    public static string TokenInfo(string tokenId) => $"/api/tokens/{tokenId}";          // GET
    public static string TokenHolders(string tokenId) => $"/api/tokens/{tokenId}/holders"; // GET
}

static class HttpCommon
{
    public static HttpClient NewClient()
    {
        var http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true // dev only
        });
        http.BaseAddress = new Uri(Api.BaseUrl);
        return http;
    }

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}

// ===== DTOs that match dotOneChain =====
record WalletNewResponse(string privateKeyPem, string publicKeyPem, string address);

record TokenCreateResult(string tokenId, string metadataCid);

// Mint body: { tokenId, toAddress, quantity }
record MintBody(string tokenId, string toAddress, long quantity);

// Transfer body: { fromAddress, toAddress, quantity, publicKeyPem, signatureBase64, ts }
record TransferBody(string fromAddress, string toAddress, long quantity, string publicKeyPem, string signatureBase64, long ts);

// Burn body: { ownerAddress, quantity, publicKeyPem, signatureBase64, ts }
record BurnBody(string ownerAddress, long quantity, string publicKeyPem, string signatureBase64, long ts);

class Program
{
    static async Task Main(string[] args)
    {
        // quick demo flow if you run without args:
        // 1) create wallets; 2) create token; 3) mint; 4) transfer; 5) burn
        if (args.Length == 0)
        {
             await DemoFlow();
        }

        Console.WriteLine("This sample is intended to be run without args for a quick demo.");
        Console.ReadLine();
    }

    static async Task<int> DemoFlow()
    {
        using var http = HttpCommon.NewClient();

        Console.WriteLine("=== dotOneChain client demo ===");

        // 1) Create two wallets (GET /api/wallets/new)
        var alice = await GetWalletNew(http);
        var bob = await GetWalletNew(http);

        Console.WriteLine("Alice:");
        Console.WriteLine(JsonSerializer.Serialize(alice, HttpCommon.Json));
        Console.WriteLine("Bob:");
        Console.WriteLine(JsonSerializer.Serialize(bob, HttpCommon.Json));

        // 2) Create an 1155 token (POST /api/tokens multipart/form-data)
        //    fields: tokenId? name description maxSupply transferable? metadataCid? mediaFile?
        string tokenName = "My 1155";
        string tokenDesc = "Demo collection";
        long maxSupply = 1000;
        string? optionalMediaPath = null; // e.g. "./image.png"

        var token = await CreateTokenMultipart(http,
            tokenId: null,
            name: tokenName,
            description: tokenDesc,
            maxSupply: maxSupply,
            transferable: true,
            metadataCid: null,
            mediaFilePath: optionalMediaPath);

        Console.WriteLine("Created token:");
        Console.WriteLine(JsonSerializer.Serialize(token, HttpCommon.Json));

        // 3) Mint quantity to Alice (POST JSON /api/nfts/mint)
        var mintResp = await PostJson(http, Api.NftMint, new MintBody(token.tokenId, alice.address, quantity: 10));
        Console.WriteLine("Mint queued: " + mintResp);

        // 4) Transfer some from Alice -> Bob (signed P-256)
        long transferQty = 3;
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string transferMsg = CanonicalTransfer(token.tokenId, alice.address, bob.address, transferQty, ts);
        string transferSig = SignWithP256(alice.privateKeyPem, transferMsg);
        var transferBody = new TransferBody(alice.address, bob.address, transferQty, alice.publicKeyPem, transferSig, ts);
        var transferResp = await PostJson(http, Api.NftTransfer(token.tokenId), transferBody);
        Console.WriteLine("Transfer queued: " + transferResp);

        // 5) Burn some from Bob (signed P-256)
        long burnQty = 2;
        long ts2 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string burnMsg = CanonicalBurn(token.tokenId, bob.address, burnQty, ts2);
        string burnSig = SignWithP256(bob.privateKeyPem, burnMsg);
        var burnBody = new BurnBody(bob.address, burnQty, bob.publicKeyPem, burnSig, ts2);
        var burnResp = await PostJson(http, Api.NftBurn(token.tokenId), burnBody);
        Console.WriteLine("Burn queued: " + burnResp);

        // (Optional) show token info & holders
        var tokInfo = await GetText(http, Api.TokenInfo(token.tokenId));
        Console.WriteLine("Token info:");
        Console.WriteLine(tokInfo);

        var holders = await GetText(http, Api.TokenHolders(token.tokenId));
        Console.WriteLine("Holders:");
        Console.WriteLine(holders);

        Console.WriteLine("=== done ===");
        return 0;
    }

    // ===== dotOneChain canonical messages =====
    static string CanonicalTransfer(string tokenId, string from, string to, long qty, long unixMs) =>
        $"NFT-TRANSFER\ntoken:{tokenId}\nfrom:{from}\nto:{to}\nqty:{qty}\nts:{unixMs}";

    static string CanonicalBurn(string tokenId, string owner, long qty, long unixMs) =>
        $"NFT-BURN\ntoken:{tokenId}\nowner:{owner}\nqty:{qty}\nts:{unixMs}";

    // ===== P-256 signing (PEM) =====
    static string SignWithP256(string privateKeyPem, string message)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        var data = Encoding.UTF8.GetBytes(message);
        var sig = ecdsa.SignData(data, HashAlgorithmName.SHA256); // DER-encoded
        return Convert.ToBase64String(sig);
    }

    // ===== HTTP helpers =====
  static async Task<WalletNewResponse> GetWalletNew(HttpClient http)
{
    // send empty JSON object {} so Content-Type = application/json
    var resp = await http.PostAsync(Api.WalletNew, 
        new StringContent("{}", Encoding.UTF8, "application/json"));

    resp.EnsureSuccessStatusCode();

    var json = await resp.Content.ReadAsStringAsync();
    var data = JsonSerializer.Deserialize<WalletNewResponse>(json, HttpCommon.Json);
    if (data == null) 
        throw new Exception("wallet/new returned empty");

    return data;
}

    static async Task<TokenCreateResult> CreateTokenMultipart(HttpClient http,
        string? tokenId,
        string name,
        string description,
        long maxSupply,
        bool transferable,
        string? metadataCid,
        string? mediaFilePath)
    {
        using var form = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(tokenId)) form.Add(new StringContent(tokenId), "tokenId");
        form.Add(new StringContent(name), "name");
        form.Add(new StringContent(description), "description");
        form.Add(new StringContent(maxSupply.ToString()), "maxSupply");
        form.Add(new StringContent(transferable.ToString().ToLowerInvariant()), "transferable");
        if (!string.IsNullOrWhiteSpace(metadataCid)) form.Add(new StringContent(metadataCid), "metadataCid");
        if (!string.IsNullOrWhiteSpace(mediaFilePath) && File.Exists(mediaFilePath))
        {
            var fs = File.OpenRead(mediaFilePath);
            var sc = new StreamContent(fs);
            sc.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(sc, "mediaFile", Path.GetFileName(mediaFilePath));
        }

        var resp = await http.PostAsync(Api.TokenCreate, form);   // <-- multipart/form-data (no 415)
        resp.EnsureSuccessStatusCode();
        var txt = await resp.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<TokenCreateResult>(txt, HttpCommon.Json)
                   ?? throw new Exception("Token create: empty response");
        return data;
    }

    static async Task<string> PostJson(HttpClient http, string url, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, HttpCommon.Json), Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(url, content);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    static async Task<string> GetText(HttpClient http, string url)
    {
        var resp = await http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }
}
