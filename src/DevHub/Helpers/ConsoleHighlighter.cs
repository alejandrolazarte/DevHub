using System.Text.RegularExpressions;

namespace DevHub.Helpers;

public static class ConsoleHighlighter
{
    private static readonly (Regex Pattern, string Color)[] Rules =
    [
        (new Regex(@"https?://\S+",                                                    RegexOptions.Compiled),                          "#60a5fa"),
        (new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d+)?\b",                RegexOptions.Compiled),                          "#34d399"),
        (new Regex(@"\blocalhost(:\d+)?(/\S*)?\b",                                     RegexOptions.Compiled | RegexOptions.IgnoreCase), "#34d399"),
        (new Regex(@"\b(error|errors|failed|fail|failure|err|exception)\b",            RegexOptions.Compiled | RegexOptions.IgnoreCase), "#f87171"),
        (new Regex(@"\b(warn|warning|warnings)\b",                                     RegexOptions.Compiled | RegexOptions.IgnoreCase), "#fbbf24"),
        (new Regex(@"\b(info|information)\b",                                          RegexOptions.Compiled | RegexOptions.IgnoreCase), "#94a3b8"),
        (new Regex(@"\b(success|succeeded|done|passed|ok|ready|started|listening)\b",  RegexOptions.Compiled | RegexOptions.IgnoreCase), "#4ade80"),
        (new Regex(@"\b\d+(\.\d+)?\s?ms\b",                                           RegexOptions.Compiled),                          "#c084fc"),
        (new Regex(@":\d{2,5}\b",                                                      RegexOptions.Compiled),                          "#34d399"),
    ];

    public static IEnumerable<(string Text, string? Color)> Tokenize(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            yield return (line, null);
            yield break;
        }

        var matches = Rules
            .SelectMany(r => r.Pattern.Matches(line).Select(m => (Match: m, Color: r.Color)))
            .OrderBy(x => x.Match.Index)
            .ThenByDescending(x => x.Match.Length)
            .ToList();

        var pos = 0;
        foreach (var (match, color) in matches)
        {
            if (match.Index < pos)
            {
                continue;
            }

            if (match.Index > pos)
            {
                yield return (line[pos..match.Index], null);
            }

            yield return (match.Value, color);
            pos = match.Index + match.Length;
        }

        if (pos < line.Length)
        {
            yield return (line[pos..], null);
        }
    }
}
