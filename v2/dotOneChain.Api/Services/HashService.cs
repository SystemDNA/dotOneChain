using System.Security.Cryptography;
using System.Text;

namespace dotOneChain.Api.Services;

public interface IHashService { string Sha256Hex(string input); }

public class HashService : IHashService
{
    public string Sha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
