namespace dotOneChain.Api.Services;

public interface IMerkleService { string ComputeMerkleRoot(IEnumerable<string> leaves); }

public class MerkleService : IMerkleService
{
    private readonly IHashService _hash;
    public MerkleService(IHashService hash) => _hash = hash;

    public string ComputeMerkleRoot(IEnumerable<string> leaves)
    {
        var nodes = leaves.Select(x => _hash.Sha256Hex(x)).ToList();
        if (nodes.Count == 0) return _hash.Sha256Hex(string.Empty);
        while (nodes.Count > 1)
        {
            var next = new List<string>();
            for (int i = 0; i < nodes.Count; i += 2)
            {
                var left = nodes[i];
                var right = (i + 1 < nodes.Count) ? nodes[i + 1] : left;
                next.Add(_hash.Sha256Hex(left + right));
            }
            nodes = next;
        }
        return nodes[0];
    }
}
