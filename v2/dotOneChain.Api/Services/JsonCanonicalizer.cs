using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

namespace dotOneChain.Api.Services
{
    public static class JsonCanonicalizer
    {
        public static string Canonicalize(string json)
        {
            var node = JsonNode.Parse(json)!;
            var ordered = OrderNode(node);
            return ordered.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }

        public static string Canonicalize(object obj)
        {
            var json = JsonSerializer.Serialize(obj);
            return Canonicalize(json);
        }

        private static JsonNode OrderNode(JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    {
                        var ordered = new JsonObject();
                        foreach (var kv in obj.OrderBy(k => k.Key, StringComparer.Ordinal))
                        {
                            // Ensure clone of child node before recursion
                            var cloned = kv.Value is null ? null : JsonNode.Parse(kv.Value.ToJsonString());
                            ordered[kv.Key] = cloned is null ? null : OrderNode(cloned);
                        }
                        return ordered;
                    }

                case JsonArray arr:
                    {
                        var newArr = new JsonArray();
                        foreach (var item in arr)
                        {
                            var clonedItem = item is null ? null : JsonNode.Parse(item.ToJsonString());
                            newArr.Add(clonedItem is null ? null : OrderNode(clonedItem));
                        }
                        return newArr;
                    }

                default:
                    // For value nodes (string, number, bool, null)
                    return JsonNode.Parse(node.ToJsonString())!;
            }
        }

        public static string Sha256Hex(string canonicalJson)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(canonicalJson);
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }
    }
}
