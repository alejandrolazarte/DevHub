namespace DevHub.Models;

public enum SymbolLanguage { CSharp, TypeScript }

public enum SymbolRole
{
    DefineClass,
    DefineInterface,
    DefineMethod,
    DefineProperty,
    Implements,
    UsesImport,
    UsesInstance,
    UsesParameter
}

public record RipgrepMatch(
    string FilePath,
    int LineNumber,
    string LineText);

public record SymbolMatch(
    string RepoPath,
    string RepoName,
    string FilePath,
    int LineNumber,
    string LineText,
    SymbolLanguage? Language,
    SymbolRole? Role);

public record SymbolSearchResult(
    string Term,
    IReadOnlyList<SymbolMatch> Matches);

public record ContextBlock(
    string FilePath,
    int FocusLineNumber,
    IReadOnlyList<ContextLine> Lines);

public record ContextLine(int LineNumber, string Text, bool IsFocus);
