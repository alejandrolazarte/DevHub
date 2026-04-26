using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevHub.Services.SecretProfiles;

public static class JsonPrettyFormatter
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string Format(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        try
        {
            var node = JsonNode.Parse(raw, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            if (node is not JsonObject obj)
            {
                return raw;
            }

            var grouped = obj
                .Select(kvp => (kvp.Key, kvp.Value))
                .GroupBy(x => GroupKey(x.Key), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sb = new StringBuilder();
            sb.Append("{\n");

            for (var gi = 0; gi < grouped.Count; gi++)
            {
                var items = grouped[gi]
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (var pi = 0; pi < items.Count; pi++)
                {
                    var (key, value) = items[pi];
                    var valueJson = value is null ? "null" : value.ToJsonString(Indented);
                    var indented = valueJson.Replace("\n", "\n  ");

                    sb.Append("  ");
                    sb.Append(JsonSerializer.Serialize(key));
                    sb.Append(": ");
                    sb.Append(indented);

                    var isLastOverall = gi == grouped.Count - 1 && pi == items.Count - 1;
                    if (!isLastOverall)
                    {
                        sb.Append(',');
                    }

                    sb.Append('\n');
                }

                if (gi < grouped.Count - 1)
                {
                    sb.Append('\n');
                }
            }

            sb.Append('}');
            return sb.ToString();
        }
        catch
        {
            return raw;
        }
    }

    private static string GroupKey(string key)
    {
        var idx = key.IndexOf(':');
        return idx > 0 ? key[..idx] : key;
    }
}
