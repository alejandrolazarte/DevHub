using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components;

public partial class CanvasContextPanel
{
    [Inject] private ICanvasRipgrepService SearchService { get; set; } = default!;
    [Inject] private CanvasVsCodeService VsCode { get; set; } = default!;

    [Parameter, EditorRequired] public required string RepoName { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<SymbolMatch> Matches { get; set; }
    [Parameter] public EventCallback<string> OnSeeReferences { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private SymbolMatch? _selected;
    private ContextBlock? _context;

    private async Task SelectMatch(SymbolMatch match)
    {
        _selected = match;
        _context = await SearchService.GetContextAsync(match.FilePath, match.LineNumber);
    }

    private async Task GoToFile()
    {
        if (_selected is not null)
        {
            await VsCode.OpenFileAsync(_selected.FilePath, _selected.LineNumber);
        }
    }

    private async Task SeeReferences()
    {
        if (_selected is not null && OnSeeReferences.HasDelegate)
        {
            await OnSeeReferences.InvokeAsync(_selected.LineText.Trim());
        }
    }

    private string MatchStyle(SymbolMatch match) =>
        match == _selected
            ? "cursor:pointer; border-color: var(--mud-palette-primary);"
            : "cursor:pointer;";

    private static string ShortPath(string filePath)
    {
        var parts = filePath.Replace('\\', '/').Split('/');
        return parts.Length > 2 ? $"…/{parts[^2]}/{parts[^1]}" : filePath;
    }

    private static Color RoleColor(SymbolRole? role) => role switch
    {
        SymbolRole.DefineClass => Color.Secondary,
        SymbolRole.DefineInterface => Color.Info,
        SymbolRole.DefineMethod => Color.Success,
        SymbolRole.DefineProperty => Color.Warning,
        SymbolRole.Implements => Color.Warning,
        SymbolRole.UsesImport or SymbolRole.UsesInstance or SymbolRole.UsesParameter => Color.Default,
        _ => Color.Default,
    };

    private static string RoleLabel(SymbolLanguage? lang, SymbolRole? role) => role switch
    {
        SymbolRole.DefineClass => lang == SymbolLanguage.CSharp ? "class" : "class",
        SymbolRole.DefineInterface => "interface",
        SymbolRole.DefineMethod => lang == SymbolLanguage.TypeScript ? "fn/component" : "method",
        SymbolRole.DefineProperty => "property",
        SymbolRole.Implements => "implements",
        SymbolRole.UsesImport => "import",
        SymbolRole.UsesInstance => "new",
        SymbolRole.UsesParameter => "parameter",
        _ => "match",
    };
}
