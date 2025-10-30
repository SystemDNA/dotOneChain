using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynamicFormsRazor.Utils
{
    public static class CanonicalJson
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static string FromObject<T>(T obj)
        {
            // Serialize to node
            var node = JsonSerializer.SerializeToNode(obj, _opts);
            var sorted = SortNode(node);
            return sorted.ToJsonString(_opts);
        }

        private static JsonNode SortNode(JsonNode? node)
        {
            if (node is null)
                return null!;

            switch (node)
            {
                case JsonObject obj:
                    {
                        var so = new JsonObject();
                        foreach (var kv in obj.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                        {
                            // Deep clone each child node before adding
                            var clonedChild = kv.Value is null ? null : JsonNode.Parse(kv.Value.ToJsonString());
                            so[kv.Key] = SortNode(clonedChild);
                        }
                        return so;
                    }

                case JsonArray arr:
                    {
                        var sa = new JsonArray();
                        foreach (var item in arr)
                        {
                            var clonedItem = item is null ? null : JsonNode.Parse(item.ToJsonString());
                            sa.Add(SortNode(clonedItem));
                        }
                        return sa;
                    }

                default:
                    // For primitive values, return a clone to avoid parent conflicts
                    return JsonNode.Parse(node.ToJsonString())!;
            }
        }
    }
}
