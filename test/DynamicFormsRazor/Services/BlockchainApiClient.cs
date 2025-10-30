using DynamicFormsRazor.Dtos;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DynamicFormsRazor.Services;

public sealed class BlockchainApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public BlockchainApiClient(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        var baseUrl = cfg.GetSection("Blockchain")["BaseUrl"] ?? "http://localhost:64925";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ----------------------------------------------------------------------
    // Wallets
    // ----------------------------------------------------------------------
    public async Task<NewWalletResponse?> NewWalletAsync()
    {
        var res = await _http.PostAsync("api/wallets/new", null);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<NewWalletResponse>(JsonOpts);
    }

    // ----------------------------------------------------------------------
    // Token creation (ObjectNFT)
    // ----------------------------------------------------------------------
    public async Task<CreateTokenResponse?> CreateTokenAsync(CreateTokenRequest req)
    {
        var res = await _http.PostAsJsonAsync("api/tokens", req, JsonOpts);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CreateTokenResponse>(JsonOpts);
    }

    public async Task<TokenSummary?> GetTokenAsync(string tokenId)
    {
        var res = await _http.GetAsync($"api/tokens/{tokenId}");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<TokenSummary>(JsonOpts);
    }

    // ----------------------------------------------------------------------
    // Object Update
    // ----------------------------------------------------------------------
    public async Task<EnqueueResponse?> UpdateObjectAsync(string tokenId, UpdateObjectRequest req)
    {
        var res = await _http.PostAsJsonAsync($"api/tokens/{tokenId}/object", req, JsonOpts);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<EnqueueResponse>(JsonOpts);
    }

    public async Task<bool> FreezeAsync(string tokenId, string controllerPublicKeyPem)
    {
        var res = await _http.PostAsJsonAsync($"api/tokens/{tokenId}/freeze", new { controllerPublicKeyPem }, JsonOpts);
        return res.IsSuccessStatusCode;
    }

    // ----------------------------------------------------------------------
    // Supply ops (Mint / Burn / Transfer)
    // ----------------------------------------------------------------------
    public async Task<EnqueueResponse?> MintAsync(MintRequest req)
    {
        var res = await _http.PostAsJsonAsync("api/nfts/mint", req, JsonOpts);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<EnqueueResponse>(JsonOpts);
    }

    public async Task<EnqueueResponse?> BurnAsync(BurnRequest req)
    {
        var res = await _http.PostAsJsonAsync($"api/nfts/{req.tokenId}/burn", req, JsonOpts);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<EnqueueResponse>(JsonOpts);
    }

    public async Task<EnqueueResponse?> TransferAsync(string tokenId, TransferRequest req)
    {
        var res = await _http.PostAsJsonAsync($"api/nfts/{tokenId}/transfer", req, JsonOpts);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<EnqueueResponse>(JsonOpts);
    }

    // ----------------------------------------------------------------------
    // Tx lookup + CID tools
    // ----------------------------------------------------------------------
    public async Task<Tx1155?> GetTransactionAsync(string id)
    {
        var res = await _http.GetAsync($"api/transactions/{id}");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<Tx1155>(JsonOpts);
    }

    public async Task<CalcCidResponse?> CalcCidAsync(CalcCidRequest req)
    {
        var res = await _http.PostAsJsonAsync("api/tools/calc-cid", req, JsonOpts);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CalcCidResponse>(JsonOpts);
    }
}
