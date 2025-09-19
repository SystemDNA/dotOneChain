using System.Security.Cryptography;
using System.Text;

namespace dotOneChain.Api.Services;

public interface ICryptoService
{
    bool VerifySignature(string publicKeyPem, string message, string signatureBase64);
    string SignWithPrivateKey(string privateKeyPem, string message);
    string CanonicalTransfer(string tokenId, string from, string to, long qty, long unixMs);
    string CanonicalBurn(string tokenId, string owner, long qty, long unixMs);
    string CanonicalUpdateObject(string tokenId, string newCid, string prevCid, int newVersion, string jsonSha256, long unixMs);
    string DeriveAddress(string publicKeyPem);
}

public class CryptoService : ICryptoService
{
    public bool VerifySignature(string publicKeyPem, string message, string signatureBase64)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(publicKeyPem);
        var data = Encoding.UTF8.GetBytes(message);
        var sig = Convert.FromBase64String(signatureBase64);
        return ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256);
    }

    public string SignWithPrivateKey(string privateKeyPem, string message)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        var data = Encoding.UTF8.GetBytes(message);
        var sig = ecdsa.SignData(data, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(sig);
    }

    public string CanonicalTransfer(string tokenId, string from, string to, long qty, long unixMs)
        => $"NFT-TRANSFER\ntoken:{tokenId}\nfrom:{from}\nto:{to}\nqty:{qty}\nts:{unixMs}";

    public string CanonicalBurn(string tokenId, string owner, long qty, long unixMs)
        => $"NFT-BURN\ntoken:{tokenId}\nowner:{owner}\nqty:{qty}\nts:{unixMs}";

    public string CanonicalUpdateObject(string tokenId, string newCid, string prevCid, int newVersion, string jsonSha256, long unixMs)
        => $"NFT-UPDATE-OBJECT\ntoken:{tokenId}\nnew:{newCid}\nprev:{prevCid}\nver:{newVersion}\nsha:{jsonSha256}\nts:{unixMs}";

    public string DeriveAddress(string publicKeyPem)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(publicKeyPem);
        var spki = ecdsa.ExportSubjectPublicKeyInfo();
        using var sha = SHA256.Create();
        var h = sha.ComputeHash(spki);
        return Convert.ToHexString(h.AsSpan(0, 20)).ToLowerInvariant();
    }
}
