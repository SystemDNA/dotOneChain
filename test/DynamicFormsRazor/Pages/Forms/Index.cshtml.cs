using DynamicFormsRazor.Dtos;
using DynamicFormsRazor.Models;
using DynamicFormsRazor.Services;
using DynamicFormsRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DynamicFormsRazor.Pages.Forms;

public class IndexModel : PageModel
{
    private readonly FormDefinitionRepository _repo;
    private readonly BlockchainApiClient _bc;

    public List<FormDefinition> Items { get; set; } = new();

    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    public string? Sender => HttpContext.Session.GetObject<string>("Sender");
    public string? Receiver => HttpContext.Session.GetObject<string>("Receiver");
    public string? TokenId => HttpContext.Session.GetObject<string>("TokenId");

    public IndexModel(FormDefinitionRepository repo, BlockchainApiClient bc)
    {
        _repo = repo;
        _bc = bc;
    }

    public async Task OnGetAsync()
    {
        Items = await _repo.ListCurrentsAsync();
    }

    // ------------------------------------------------------------------
    // Wallet creation
    // ------------------------------------------------------------------
    public async Task<IActionResult> OnPostNewWallet()
    {
        try
        {
            var w1 = await _bc.NewWalletAsync() ?? throw new("Wallet creation failed (sender)");
            var w2 = await _bc.NewWalletAsync() ?? throw new("Wallet creation failed (receiver)");

            HttpContext.Session.SetObject("SenderPrivate", w1.privateKeyPem);
            HttpContext.Session.SetObject("SenderPublic", w1.publicKeyPem);
            HttpContext.Session.SetObject("Sender", w1.address);
            HttpContext.Session.SetObject("ReceiverPrivate", w2.privateKeyPem);
            HttpContext.Session.SetObject("ReceiverPublic", w2.publicKeyPem);
            HttpContext.Session.SetObject("Receiver", w2.address);

            Message = $"✅ Wallets created successfully — Sender: {w1.address}, Receiver: {w2.address}";
        }
        catch (Exception ex)
        {
            Error = $"❌ Wallet creation failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    // ------------------------------------------------------------------
    // Create Token
    // ------------------------------------------------------------------
    public async Task<IActionResult> OnPostCreateToken(string id)
    {
        try
        {
            var def = await _repo.GetAsync(id) ?? throw new("Definition not found");
            var senderPublic = HttpContext.Session.GetObject<string>("SenderPublic");

            if (string.IsNullOrWhiteSpace(senderPublic))
                return RedirectWithError("Please create wallets first.");

            var canonical = CanonicalJson.FromObject(def);

            var create = await _bc.CreateTokenAsync(new CreateTokenRequest(
                tokenId: null,
                name: def.Name,
                description: $"Form definition {def.FormKey} v{def.Version}",
                maxSupply: 100_000,
                transferable: true,
                controllerPublicKeyPem: senderPublic!,
                objectJson: canonical
            ));

            if (create is null) return RedirectWithError("Token creation failed.");

            HttpContext.Session.SetObject("TokenId", create.tokenId);
            Message = $"✅ Token created successfully — ID: {create.tokenId}";
        }
        catch (Exception ex)
        {
            Error = $"❌ Token creation failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    // ------------------------------------------------------------------
    // Mint
    // ------------------------------------------------------------------
    public async Task<IActionResult> OnPostMint(string id, int amount)
    {
        var tokenId = HttpContext.Session.GetObject<string>("TokenId");
        var sender = HttpContext.Session.GetObject<string>("Sender");

        if (string.IsNullOrWhiteSpace(tokenId))
            return RedirectWithError("Create token first.");
        if (string.IsNullOrWhiteSpace(sender))
            return RedirectWithError("Create wallets first.");

        try
        {
            var enq = await _bc.MintAsync(new MintRequest(tokenId!, sender!, amount));
            if (enq is null || !enq.queued) return RedirectWithError("Mint failed.");

            Message = $"✅ Minted {amount} — txId: {enq.txId}";
        }
        catch (Exception ex)
        {
            Error = $"❌ Mint failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    // ------------------------------------------------------------------
    // Transfer
    // ------------------------------------------------------------------
    public async Task<IActionResult> OnPostTransfer(string id, int amount)
    {
        var tokenId = HttpContext.Session.GetObject<string>("TokenId");
        var sender = HttpContext.Session.GetObject<string>("Sender");
        var senderPublic = HttpContext.Session.GetObject<string>("SenderPublic");
        var senderPrivate = HttpContext.Session.GetObject<string>("SenderPrivate");
        var receiver = HttpContext.Session.GetObject<string>("Receiver");

        if (string.IsNullOrWhiteSpace(tokenId))
            return RedirectWithError("Create token first.");
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(receiver) || string.IsNullOrWhiteSpace(senderPrivate))
            return RedirectWithError("Create wallets first.");

        try
        {
            // Prepare canonical transfer message (same format used in ObjectNFT)
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var message = $"NFT-TRANSFER\ntoken:{tokenId}\nfrom:{sender}\nto:{receiver}\nqty:{amount}\nts:{ts}";

            // Sign the message using sender’s private key
            var signature = Crypto.Sign(senderPrivate!, message);

            // Create TransferRequest with all required parameters
            var req = new TransferRequest(
                fromAddress: sender!,
                toAddress: receiver!,
                quantity: amount,
                publicKeyPem: senderPublic!,
                signatureBase64: signature,
                ts: ts
            );

            // Submit transfer request to blockchain node
            var enq = await _bc.TransferAsync(tokenId!, req);
            if (enq is null || !enq.queued)
                return RedirectWithError("Transfer failed or not queued.");

            Message = $"✅ Transferred {amount} successfully — txId: {enq.txId}";
        }
        catch (Exception ex)
        {
            Error = $"❌ Transfer failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    // ------------------------------------------------------------------
    // Burn
    // ------------------------------------------------------------------
    public async Task<IActionResult> OnPostBurn(string id, int amount)
    {
        var tokenId = HttpContext.Session.GetObject<string>("TokenId");
        var receiver = HttpContext.Session.GetObject<string>("Receiver");
        var receiverPublic = HttpContext.Session.GetObject<string>("ReceiverPublic");
        var receiverPrivate = HttpContext.Session.GetObject<string>("ReceiverPrivate");

        if (string.IsNullOrWhiteSpace(tokenId))
            return RedirectWithError("Create token first.");
        if (string.IsNullOrWhiteSpace(receiver) || string.IsNullOrWhiteSpace(receiverPrivate))
            return RedirectWithError("Create wallets first.");

        try
        {
            // Prepare canonical message for burn
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var message = $"NFT-BURN\ntoken:{tokenId}\nowner:{receiver}\nqty:{amount}\nts:{ts}";

            // Sign the message using receiver’s private key
            var signature = Crypto.Sign(receiverPrivate!, message);

            // Build the request with full parameters
            var req = new BurnRequest(
                tokenId: tokenId!,
                ownerAddress: receiver!,
                quantity: amount,
                publicKeyPem: receiverPublic!,
                signatureBase64: signature,
                ts: ts
            );

            var enq = await _bc.BurnAsync(req);
            if (enq is null || !enq.queued)
                return RedirectWithError("Burn failed or not queued.");

            Message = $"🔥 Burned {amount} successfully — txId: {enq.txId}";
        }
        catch (Exception ex)
        {
            Error = $"❌ Burn failed: {ex.Message}";
        }

        return RedirectToPage();
    }
    private IActionResult RedirectWithError(string msg)
    {
        Error = msg;
        return RedirectToPage();
    }
}
