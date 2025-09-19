using System.Security.Cryptography;
using System.Text;

namespace ObjectNFT.TestClient;

public static class Crypto
{
    public static string DeriveAddress(string publicKeyPem)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(publicKeyPem);
        var spki = ecdsa.ExportSubjectPublicKeyInfo();
        using var sha = SHA256.Create();
        var h = sha.ComputeHash(spki);
        return Convert.ToHexString(h.AsSpan(0, 20)).ToLowerInvariant();
    }

    public static (string privatePem, string publicPem, string address) NewKeypair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePem = ecdsa.ExportPkcs8PrivateKeyPem();
        var publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var addr = DeriveAddress(publicPem);
        return (privatePem, publicPem, addr);
    }

    public static string Sign(string privateKeyPem, string message)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        var data = Encoding.UTF8.GetBytes(message);
        var sig = ecdsa.SignData(data, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(sig);
    }

    public static string CanonicalTransfer(string tokenId, string from, string to, long qty, long unixMs)
        => $"NFT-TRANSFER\ntoken:{tokenId}\nfrom:{from}\nto:{to}\nqty:{qty}\nts:{unixMs}";

    public static string CanonicalBurn(string tokenId, string owner, long qty, long unixMs)
        => $"NFT-BURN\ntoken:{tokenId}\nowner:{owner}\nqty:{qty}\nts:{unixMs}";

    public static string CanonicalUpdateObject(string tokenId, string newCid, string prevCid, int newVersion, string jsonSha256, long unixMs)
        => $"NFT-UPDATE-OBJECT\ntoken:{tokenId}\nnew:{newCid}\nprev:{prevCid}\nver:{newVersion}\nsha:{jsonSha256}\nts:{unixMs}";
}
