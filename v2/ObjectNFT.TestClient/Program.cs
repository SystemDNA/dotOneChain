using System.Text.Json;

namespace ObjectNFT.TestClient;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var cfg = JsonDocument.Parse(File.ReadAllText("appsettings.json")).RootElement;
        var baseUrl = cfg.GetProperty("baseUrl").GetString() ?? "http://localhost:5000";
        var pollIntervalMs = cfg.GetProperty("poll").GetProperty("intervalMs").GetInt32();
        var pollTimeoutMs = cfg.GetProperty("poll").GetProperty("timeoutMs").GetInt32();
        var api = new HttpApi(baseUrl);

        Console.WriteLine("== ObjectNFT Test Client (End-to-End) ==");
        Console.WriteLine($"Base URL: {baseUrl}");
        Console.WriteLine();

        // ------------------------------------------------------------------
        // 1) Create wallets: sender (also controller) and receiver
        // ------------------------------------------------------------------
        var sender = await api.NewWalletAsync();   // returns private, public, address
        var receiver = await api.NewWalletAsync();
        Console.WriteLine($"Sender   : {sender.address}");
        Console.WriteLine($"Receiver : {receiver.address}");
        Console.WriteLine();

        // ------------------------------------------------------------------
        // 2) Create token (v1) where Object JSON IS the NFT; controller = sender
        // ------------------------------------------------------------------
        var objectJsonV1 = JsonSerializer.Serialize(new
        {
            BOID = "100006",
            Name = "XIAIAssistant",
            UpdatedTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Note = "Version 1 - created from test client"
        });

        var createReq = new CreateTokenRequest(
            tokenId: null,
            name: "MyObjectNFT",
            description: "Object JSON as NFT",
            maxSupply: 1,
            transferable: true,
            controllerPublicKeyPem: sender.publicKeyPem,
            objectJson: objectJsonV1
        );

        var created = await api.CreateTokenAsync(createReq) ?? throw new Exception("Create token failed");
        Console.WriteLine($"TokenId: {created.tokenId}");
        Console.WriteLine($"v1 CID:  {created.objectCid}");
        Console.WriteLine($"SHA:     {created.sha}");
        Console.WriteLine();

        // ------------------------------------------------------------------
        // 3) Mint 1 to sender
        // ------------------------------------------------------------------
        var mintEnq = await api.MintAsync(new MintRequest(created.tokenId, sender.address, 1));
        Console.WriteLine($"Mint queued: {mintEnq?.queued} (txId: {mintEnq?.txId})");
        await WaitForTxId(api, mintEnq?.txId!, pollIntervalMs, pollTimeoutMs, "mint committed");

        // Wait until sender portfolio shows 1 of the token
        await WaitUntil(async () =>
        {
            var p = await api.GetPortfolioAsync(sender.address);
            var has = p?.items?.Any(i => i.tokenId == created.tokenId && i.balance >= 1) ?? false;
            return has;
        }, pollIntervalMs, pollTimeoutMs, "mint applied");

        // ------------------------------------------------------------------
        // 4) UpdateObject to v2 (preflight -> sign -> send) by controller (sender)
        // ------------------------------------------------------------------
        var prevCid = created.objectCid;
        var v2Obj = JsonSerializer.Serialize(new
        {
            BOID = "100006",
            Name = "XIAIAssistant",
            UpdatedTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Note = "Version 2 - changed one field"
        });

        var v2Canonical = JsonCanonicalizer.Canonicalize(v2Obj);

        // Preflight: compute final CID+SHA, then sign
        var pre = await api.CalcCidAsync(new CalcCidRequest(v2Canonical, $"{created.tokenId}-v2.json", 2))
                  ?? throw new Exception("Preflight calc-cid failed");
        Console.WriteLine($"Preflight CID: {pre.cid}");
        Console.WriteLine($"Preflight SHA: {pre.sha}");

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var msg2 = Crypto.CanonicalUpdateObject(created.tokenId, pre.cid, prevCid, 2, pre.sha, ts);
        var sig2 = Crypto.Sign(sender.privateKeyPem, msg2);

        var updReq = new UpdateObjectRequest(
            publicKeyPem: sender.publicKeyPem,
            signatureBase64: sig2,
            newObjectJson: v2Canonical,
            previousObjectCid: prevCid,
            newVersion: 2,
            ts: ts
        );

        var upd = await api.UpdateObjectAsync(created.tokenId, updReq);
        Console.WriteLine($"Update queued: {upd?.queued}, v2 CID: {upd?.newCid}, sha: {upd?.sha}, txId: {upd?.txId}");
        await WaitForTxId(api, upd?.txId!, pollIntervalMs, pollTimeoutMs, "update committed");

        // Wait until token shows version 2 and current CID = pre.cid
        await WaitUntil(async () =>
        {
            var t = await api.GetTokenAsync(created.tokenId);
            return t is not null && t.CurrentVersion >= 2 && string.Equals(t.CurrentObjectCid, pre.cid, StringComparison.Ordinal);
        }, pollIntervalMs, pollTimeoutMs, "object update applied");

        // ------------------------------------------------------------------
        // 5) Transfer 1 from sender -> receiver (signed by sender)
        // ------------------------------------------------------------------
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var transferMsg = Crypto.CanonicalTransfer(created.tokenId, sender.address, receiver.address, 1, nowMs);
        var transferSig = Crypto.Sign(sender.privateKeyPem, transferMsg);
        var transferEnq = await api.TransferAsync(created.tokenId,
            new TransferRequest(sender.address, receiver.address, 1, sender.publicKeyPem, transferSig, nowMs));
        Console.WriteLine($"Transfer queued: {transferEnq?.queued} (txId: {transferEnq?.txId})");
        await WaitForTxId(api, transferEnq?.txId!, pollIntervalMs, pollTimeoutMs, "transfer committed");

        // Wait until receiver portfolio shows the token
        await WaitUntil(async () =>
        {
            var p = await api.GetPortfolioAsync(receiver.address);
            var has = p?.items?.Any(i => i.tokenId == created.tokenId && i.balance >= 1) ?? false;
            return has;
        }, pollIntervalMs, pollTimeoutMs, "transfer applied");


// ------------------------------------------------------------------
// 6) Burn 1 from receiver (signed by receiver)
// ------------------------------------------------------------------
//var burnTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//var burnMsg = Crypto.CanonicalBurn(created.tokenId, receiver.address, 1, burnTs);
//var burnSig = Crypto.Sign(receiver.privateKeyPem, burnMsg);
//var burnEnq = await api.BurnAsync(created.tokenId, new BurnRequest(receiver.address, 1, receiver.publicKeyPem, burnSig, burnTs));
//Console.WriteLine($"Burn queued: {burnEnq?.queued} (txId: {burnEnq?.txId})");
//await WaitForTxId(api, burnEnq?.txId!, pollIntervalMs, pollTimeoutMs, "burn committed");

// ------------------------------------------------------------------
// 7) Freeze the token (controller only) and prove immutability
// ------------------------------------------------------------------
var froze = await api.FreezeAsync(created.tokenId, sender.publicKeyPem);
Console.WriteLine($"Freeze requested: {froze}");

// Try to push a v3 update (should NOT commit after freeze)
var v3Obj = JsonSerializer.Serialize(new {
    BOID = "100006",
    Name = "XIAIAssistant",
    UpdatedTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
    Note = "Version 3 - should be blocked by freeze"
});
var v3Canonical = JsonCanonicalizer.Canonicalize(v3Obj);
var pre3 = await api.CalcCidAsync(new CalcCidRequest(v3Canonical, $"{created.tokenId}-v3.json", 3))
          ?? throw new Exception("Preflight v3 failed");
var ts3 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var msg3 = Crypto.CanonicalUpdateObject(created.tokenId, pre3.cid, pre.cid /* still previous current */, 3, pre3.sha, ts3);
var sig3 = Crypto.Sign(sender.privateKeyPem, msg3);
var upd3Enq = await api.UpdateObjectAsync(created.tokenId, new UpdateObjectRequest(
    publicKeyPem: sender.publicKeyPem,
    signatureBase64: sig3,
    newObjectJson: v3Canonical,
    previousObjectCid: pre.cid,
    newVersion: 3,
    ts: ts3
));
Console.WriteLine($"(Frozen) Update v3 queued? {upd3Enq?.queued} (txId: {upd3Enq?.txId})");

// Wait for v3 tx briefly; it should NOT appear (validate will fail after freeze)
try
{
    await WaitForTxId(api, upd3Enq?.txId!, pollIntervalMs, 5000, "frozen update SHOULD NOT commit");
    Console.WriteLine("WARNING: v3 update appeared, freeze may not be enforced in validation.");
}
catch (TimeoutException)
{
    Console.WriteLine("OK: v3 update did not commit (freeze enforced).");
}

        // ------------------------------------------------------------------
        // 6) Final read
        // ------------------------------------------------------------------
        var token = await api.GetTokenAsync(created.tokenId);
        Console.WriteLine($"FINAL -> Version: {token?.CurrentVersion}, Current CID: {token?.CurrentObjectCid}");
        Console.WriteLine($"Receiver now holds token? { (await api.GetPortfolioAsync(receiver.address))?.items?.Any(i => i.tokenId == created.tokenId) }");

        Console.WriteLine("\nEnd-to-end flow complete.");
        return 0;
    }


static async Task WaitForTxId(HttpApi api, string txId, int intervalMs, int timeoutMs, string label)
{
    var start = DateTime.UtcNow;
    while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
    {
        var tx = await api.GetTransactionAsync(txId);
        if (tx is not null)
        {
            Console.WriteLine($"OK: {label} (tx found)");
            return;
        }
        await Task.Delay(intervalMs);
    }
    throw new TimeoutException($"Timeout waiting for txId {txId} -> {label}");
}

    // Simple poll helper
    static async Task WaitUntil(Func<Task<bool>> predicate, int intervalMs, int timeoutMs, string label)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (await predicate()) { Console.WriteLine($"OK: {label}"); return; }
            await Task.Delay(intervalMs);
        }
        throw new TimeoutException($"Timeout waiting for {label}");
    }
}
