using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DevHub.Models;

namespace DevHub.Services;

public static class SymbolClassifier
{
    private sealed record CompiledRules(
        (Regex Pattern, SymbolRole Role)[] CSharp,
        (Regex Pattern, SymbolRole Role)[] TypeScript);

    private static readonly ConcurrentDictionary<string, CompiledRules> _cache = new();

    public static (SymbolLanguage Language, SymbolRole Role)? Classify(string filePath, string lineText, string term)
    {
        var language = DetectLanguage(filePath);
        if (language is null)
        {
            return null;
        }

        var escaped = Regex.Escape(term);
        var rules = _cache.GetOrAdd(escaped, BuildRules);
        var applicable = language == SymbolLanguage.CSharp ? rules.CSharp : rules.TypeScript;

        foreach (var (pattern, role) in applicable)
        {
            if (pattern.IsMatch(lineText))
            {
                return (language.Value, role);
            }
        }

        return null;
    }

    public static SymbolLanguage? DetectLanguage(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".cs" => SymbolLanguage.CSharp,
            ".ts" or ".tsx" => SymbolLanguage.TypeScript,
            _ => null
        };

    private static CompiledRules BuildRules(string escaped) =>
        new(BuildCSharpRules(escaped), BuildTypeScriptRules(escaped));

    private static (Regex, SymbolRole)[] BuildCSharpRules(string t) =>
    [
        (R($@"\b(class|record|enum)\s+{t}\b"), SymbolRole.DefineClass),
        (R($@"\binterface\s+{t}\b"), SymbolRole.DefineInterface),
        (R($@"\b\w[\w<>\[\]?,\s]*\s+{t}\s*\("), SymbolRole.DefineMethod),
        (R($@"\b\w[\w<>\[\]?,\s]*\s+{t}\s*(=>|\{{)"), SymbolRole.DefineProperty),
        (R($@":\s*[^{{]*\b{t}\b"), SymbolRole.Implements),
        (R($@"\bnew\s+{t}\s*[<({{]"), SymbolRole.UsesInstance),
        (R($@"\b{t}\s+\w+"), SymbolRole.UsesParameter),
    ];

    private static (Regex, SymbolRole)[] BuildTypeScriptRules(string t) =>
    [
        (R($@"\binterface\s+{t}\b"), SymbolRole.DefineInterface),
        (R($@"\btype\s+{t}\s*="), SymbolRole.DefineInterface),
        (R($@"\bclass\s+{t}\b"), SymbolRole.DefineClass),
        (R($@"\bfunction\s+{t}\b"), SymbolRole.DefineMethod),
        (R($@"\bconst\s+{t}\s*=\s*(\(|React\.)"), SymbolRole.DefineMethod),
        (R($@"\b(implements|extends)\s+{t}\b"), SymbolRole.Implements),
        (R($@"\bimport\b[^;]*\b{t}\b"), SymbolRole.UsesImport),
    ];

    private static Regex R(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
}
