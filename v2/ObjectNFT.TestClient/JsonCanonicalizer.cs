using System.Text.Json;
using System.Text.Json.Nodes;

namespace ObjectNFT.TestClient;

public static class JsonCanonicalizer
{
    public static string Canonicalize(string json)
    {
        var node = JsonNode.Parse(json)!;
        var ordered = OrderNode(node);
        return ordered.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonNode OrderNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var ordered = new JsonObject();
            foreach (var kv in obj.OrderBy(k => k.Key, StringComparer.Ordinal))
                ordered[kv.Key] = OrderNode(kv.Value!);
            return ordered;
        }
        if (node is JsonArray arr)
        {
            var newArr = new JsonArray();
            foreach (var item in arr)
                newArr.Add(OrderNode(item!));
            return newArr;
        }
        return node.DeepClone();
    }

    public static string Sha256Hex(string canonicalJson)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(canonicalJson);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}
