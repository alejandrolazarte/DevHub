using DevHub.Components;
using DevHub.Helpers;
using DevHub.Models;
using DevHub.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class Home : IDisposable
{
    private bool _catalogBusy;
    private FilterCriteria _filter = new(string.Empty, string.Empty, string.Empty);
    private HashSet<string> _selectedPaths = [];
    private IReadOnlyList<RepoGroup> _filteredGroups = [];
    private IReadOnlyList<RepoInfo> _filteredRepos = [];
    private IReadOnlyList<RepoInfo> _selectedRepos = [];
    private IReadOnlyList<string> _allGroups = [];

    private RepoTerminalPanel _terminalPanel = default!;

    [Inject] IDialogService DialogService { get; set; } = default!;

    protected override void OnInitialized()
    {
        Store.OnStateChanged += OnStateChanged;
        RefreshView();
    }

    private void OnStateChanged()
    {
        RefreshView();
        InvokeAsync(StateHasChanged);
    }

    private void RefreshView()
    {
        var filtered = ApplyCriteria(Store.Repos, _filter);
        _allGroups = Store.Repos.Select(r => r.Group).Distinct().OrderBy(g => g).ToList();
        _filteredRepos = filtered;
        _filteredGroups = filtered
            .GroupBy(r => r.Group)
            .OrderBy(g => g.Key)
            .Select(g => new RepoGroup(g.Key, g.First().GroupColor, g.ToList()))
            .ToList();
        _selectedRepos = _selectedPaths.Count > 0
            ? filtered.Where(r => _selectedPaths.Contains(r.Path)).ToList()
            : [];
    }

    private void ApplyFilter(FilterCriteria criteria)
    {
        _filter = criteria;
        RefreshView();
    }

    private static List<RepoInfo> ApplyCriteria(
        IReadOnlyList<RepoInfo> repos, FilterCriteria f)
    {
        return repos
            .Where(r => string.IsNullOrEmpty(f.Search) ||
                        r.Name.Contains(f.Search, StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrEmpty(f.Group) || r.Group == f.Group)
            .Where(r => f.Status switch
            {
                "dirty" => r.IsDirty,
                "clean" => !r.IsDirty,
                "behind" => r.BehindCount > 0,
                "feature" => r.Branch is not ("master" or "main") && !string.IsNullOrEmpty(r.Branch),
                _ => true
            })
            .ToList();
    }

    private Task ManualRefresh()
    {
        Scanner.RequestManualRefresh();
        return Task.CompletedTask;
    }

    private async Task PickAndAddRepoAsync()
    {
        var path = await OpenFolderPickerAsync();
        if (path is null)
        {
            return;
        }

        _catalogBusy = true;
        try
        {
            if (await Snackbar.TryAsync(
                () => RepoCatalog.AddAsync(path, CancellationToken.None),
                $"Repo agregado al catálogo: {path}"))
            {
                await ManualRefresh();
            }
        }
        finally
        {
            _catalogBusy = false;
        }
    }

    private async Task PickAndImportRootAsync()
    {
        var path = await OpenFolderPickerAsync();
        if (path is null)
        {
            return;
        }

        _catalogBusy = true;
        try
        {
            var imported = await RepoCatalog.ImportFromRootAsync(path, CancellationToken.None);
            Snackbar.Add($"Importados {imported} repos desde {path}.",
                imported > 0 ? Severity.Success : Severity.Info);
            await ManualRefresh();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _catalogBusy = false;
        }
    }

    private Task<string?> OpenFolderPickerAsync(string? initialPath = null)
    {
        var parameters = new DialogParameters<FolderPickerDialog>
        {
            { x => x.InitialPath, initialPath ?? string.Empty }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        return DialogService.ShowAndGetAsync<FolderPickerDialog, string>("Seleccionar carpeta", parameters, options);
    }

    private async Task RemoveRepoAsync(RepoInfo repo)
    {
        await RepoCatalog.RemoveAsync(repo.Path, CancellationToken.None);
        _selectedPaths.Remove(repo.Path);
        Snackbar.Add($"Repo quitado del catálogo: {repo.Name}", Severity.Info);
        await ManualRefresh();
    }

    private Task OpenTerminalAsync(RepoInfo repo) =>
        _terminalPanel.OpenForRepoAsync(repo);

    private void OnSelectAllChanged(bool selectAll)
    {
        _selectedPaths = selectAll
            ? _filteredRepos.Select(r => r.Path).ToHashSet()
            : [];
        RefreshView();
    }

    private void OnSelectedPathsChanged(HashSet<string> paths)
    {
        _selectedPaths = paths;
        RefreshView();
    }

    public void Dispose()
    {
        Store.OnStateChanged -= OnStateChanged;
        GC.SuppressFinalize(this);
    }
}