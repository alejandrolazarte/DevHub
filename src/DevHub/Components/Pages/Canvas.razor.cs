using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class Canvas : IAsyncDisposable
{
    [Inject] private ICanvasRipgrepService SearchService { get; set; } = default!;
    [Inject] private ICanvasService CanvasService { get; set; } = default!;
    [Inject] private IRepoCatalogService RepoCatalog { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private DotNetObjectReference<Canvas>? _dotNetRef;
    private bool _jsReady;

    private string _searchTerm = string.Empty;
    private bool _isSearching;
    private SymbolSearchResult? _lastResult;

    private IReadOnlyList<CanvasBoard> _savedCanvases = [];
    private int? _activeCanvasId;

    private string? _panelRepoPath;
    private string _panelRepoName = string.Empty;
    private IReadOnlyList<SymbolMatch> _panelMatches = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("canvasInterop.init", "cy-container", _dotNetRef);
        _jsReady = true;

        _savedCanvases = await CanvasService.GetAllAsync();
        StateHasChanged();
    }

    private async Task OnCanvasSelected(int? canvasId)
    {
        _activeCanvasId = canvasId;
        if (canvasId is null || !_jsReady)
        {
            return;
        }

        var canvas = await CanvasService.GetByIdAsync(canvasId.Value);
        if (canvas is null)
        {
            return;
        }

        await JS.InvokeVoidAsync("canvasInterop.loadGraph", canvas.CytoscapeJson);
    }

    private async Task CreateCanvas()
    {
        var name = $"Canvas {DateTime.Now:dd/MM HH:mm}";
        var canvas = await CanvasService.CreateAsync(name);
        _savedCanvases = await CanvasService.GetAllAsync();
        _activeCanvasId = canvas.Id;
        StateHasChanged();
    }

    private async Task SaveCanvas()
    {
        if (_activeCanvasId is null || !_jsReady)
        {
            return;
        }

        var json = await JS.InvokeAsync<string>("canvasInterop.getGraph");
        await CanvasService.SaveGraphAsync(_activeCanvasId.Value, json);
        Snackbar.Add("Canvas guardado", Severity.Success);
    }

    private async Task AddLabelNode()
    {
        if (_jsReady)
        {
            await JS.InvokeVoidAsync("canvasInterop.addLabelNode");
        }
    }

    private async Task FitAll()
    {
        if (_jsReady)
        {
            await JS.InvokeVoidAsync("canvasInterop.fitAll");
        }
    }

    private async Task OnSearchKeyUp(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await RunSearch();
        }
    }

    private async Task RunSearch()
    {
        if (string.IsNullOrWhiteSpace(_searchTerm) || _isSearching)
        {
            return;
        }

        _isSearching = true;
        StateHasChanged();

        try
        {
            var repoPaths = await RepoCatalog.GetRepoPathsAsync();
            _lastResult = await SearchService.SearchAsync(_searchTerm, repoPaths);

            if (_jsReady)
            {
                var elements = BuildCytoscapeElements(_lastResult);
                await JS.InvokeVoidAsync("canvasInterop.setGraph", elements);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSearching = false;
            StateHasChanged();
        }
    }

    private async Task OnSeeReferences(string symbolName)
    {
        _searchTerm = symbolName;
        await RunSearch();
    }

    [JSInvokable]
    public Task OnNodeClicked(string repoPath, string label = "")
    {
        if (string.IsNullOrEmpty(repoPath))
        {
            ClosePanel();
            return Task.CompletedTask;
        }

        var matches = _lastResult?.Matches.Where(m => m.RepoPath == repoPath).ToList()
                      ?? [];
        _panelRepoPath = repoPath;
        _panelRepoName = matches.FirstOrDefault()?.RepoName
                         ?? (string.IsNullOrEmpty(label) ? Path.GetFileName(repoPath) : label);
        _panelMatches = matches;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void ClosePanel()
    {
        _panelRepoPath = null;
        _panelMatches = [];
        StateHasChanged();
    }

    private static string BuildCytoscapeElements(SymbolSearchResult result)
    {
        var byRepo = result.Matches
            .GroupBy(m => m.RepoPath)
            .ToDictionary(g => g.Key, g => g.ToList());

        var nodes = byRepo.Select(kvp =>
        {
            var roles = kvp.Value
                .Where(m => m.Role.HasValue)
                .Select(m => m.Role!.Value.ToString())
                .Distinct()
                .ToList();

            return new
            {
                data = new
                {
                    id = kvp.Key,
                    label = kvp.Value.First().RepoName,
                    roles,
                    matchCount = kvp.Value.Count
                }
            };
        }).ToList();

        var edges = new List<object>();
        var repoList = byRepo.ToList();

        for (var i = 0; i < repoList.Count; i++)
        {
            for (var j = 0; j < repoList.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var aRoles = repoList[i].Value.Where(m => m.Role.HasValue).Select(m => m.Role!.Value).ToHashSet();
                var bRoles = repoList[j].Value.Where(m => m.Role.HasValue).Select(m => m.Role!.Value).ToHashSet();

                var label = InferEdgeLabel(aRoles, bRoles);
                if (label is not null)
                {
                    edges.Add(new
                    {
                        data = new
                        {
                            id = $"e-{i}-{j}",
                            source = repoList[i].Key,
                            target = repoList[j].Key,
                            label
                        }
                    });
                }
            }
        }

        var elements = new { nodes, edges };
        return System.Text.Json.JsonSerializer.Serialize(elements);
    }

    private static string? InferEdgeLabel(HashSet<SymbolRole> aRoles, HashSet<SymbolRole> bRoles)
    {
        var aDefines = aRoles.Contains(SymbolRole.DefineClass) || aRoles.Contains(SymbolRole.DefineInterface);
        var bImplements = bRoles.Contains(SymbolRole.Implements);
        var bUses = bRoles.Contains(SymbolRole.UsesInstance) || bRoles.Contains(SymbolRole.UsesParameter);
        var bImports = bRoles.Contains(SymbolRole.UsesImport);

        if (aDefines && bImplements)
        {
            return "implements";
        }

        if (aDefines && bUses)
        {
            return "uses";
        }

        if (aDefines && bImports)
        {
            return "imports";
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_jsReady)
        {
            await JS.InvokeVoidAsync("canvasInterop.destroy");
        }

        _dotNetRef?.Dispose();
    }
}
