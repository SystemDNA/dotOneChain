using System.Net.Http.Json;

namespace ObjectNFT.TestClient;

public partial class HttpApi
{
    private readonly HttpClient _http;
    public HttpApi(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public async Task<NewWalletResponse> NewWalletAsync()
        => (await _http.PostAsync("api/wallets/new", null)).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<NewWalletResponse>()!.Result!;

    public async Task<CreateTokenResponse?> CreateTokenAsync(CreateTokenRequest req)
    {
        var res = await _http.PostAsJsonAsync("api/tokens", req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CreateTokenResponse>();
    }

    public async Task<EnqueueResponse?> UpdateObjectAsync(string tokenId, UpdateObjectRequest req)
    {
        var res = await _http.PostAsJsonAsync($"api/tokens/{tokenId}/object", req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<EnqueueResponse>();
    }

    public async Task<TokenSummary?> GetTokenAsync(string tokenId)
    {
        var res = await _http.GetAsync($"api/tokens/{tokenId}");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<TokenSummary>();
    }

    public async Task<EnqueueResponse?> MintAsync(MintRequest req)
    {
        var r = await _http.PostAsJsonAsync("api/nfts/mint", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<EnqueueResponse>();
    }

    public async Task<EnqueueResponse?> TransferAsync(string tokenId, TransferRequest req)
    {
        var r = await _http.PostAsJsonAsync($"api/nfts/{tokenId}/transfer", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<EnqueueResponse>();
    }

    public async Task<EnqueueResponse?> BurnAsync(string tokenId, BurnRequest req)
    {
        var r = await _http.PostAsJsonAsync($"api/nfts/{tokenId}/burn", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<EnqueueResponse>();
    }

    public async Task<WalletPortfolio?> GetPortfolioAsync(string address)
    {
        var res = await _http.GetAsync($"api/wallets/{address}/portfolio");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<WalletPortfolio>();
    }

    public async Task<CalcCidResponse?> CalcCidAsync(CalcCidRequest req)
    {
        var res = await _http.PostAsJsonAsync("api/tools/calc-cid", req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CalcCidResponse>();
    }

public async Task<bool> FreezeAsync(string tokenId, string controllerPublicKeyPem)
{
    var res = await _http.PostAsJsonAsync($"api/tokens/{tokenId}/freeze", new { controllerPublicKeyPem });
    if (!res.IsSuccessStatusCode) return false;
    return true;
}

public async Task<Tx1155?> GetTransactionAsync(string id)
{
    var res = await _http.GetAsync($"api/transactions/{id}");
    if (!res.IsSuccessStatusCode) return null;
    return await res.Content.ReadFromJsonAsync<Tx1155>();
}

}
