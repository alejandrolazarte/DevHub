using DevHub.Models;

namespace DevHub.Services;

public interface ICanvasRipgrepService
{
    Task<SymbolSearchResult> SearchAsync(string term, IReadOnlyList<string> repoPaths, CancellationToken ct = default);
    Task<ContextBlock> GetContextAsync(string filePath, int lineNumber, int contextLines = 3, CancellationToken ct = default);
}
