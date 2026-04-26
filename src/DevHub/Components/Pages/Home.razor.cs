using DevHub.Components;
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
    private string _repoPathInput = string.Empty;
    private string _importRootPath = string.Empty;
    private HashSet<string> _selectedPaths = [];
    private IReadOnlyList<RepoGroup> _filteredGroups = [];
    private IReadOnlyList<RepoInfo> _filteredRepos = [];
    private IReadOnlyList<RepoInfo> _selectedRepos = [];
    private IReadOnlyList<string> _allGroups = [];
    protected override void OnInitialized()
    {
        Store.OnStateChanged += OnStateChanged;
        _importRootPath = DevHubOptions.Value.RootPath;
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

    private async Task AddRepoAsync()
    {
        if (string.IsNullOrWhiteSpace(_repoPathInput))
        {
            return;
        }

        _catalogBusy = true;
        try
        {
            await RepoCatalog.AddAsync(_repoPathInput, CancellationToken.None);
            Snackbar.Add($"Repo agregado al catálogo: {_repoPathInput}", Severity.Success);
            _repoPathInput = string.Empty;
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

    private async Task ImportRootAsync()
    {
        _catalogBusy = true;
        try
        {
            var imported = await RepoCatalog.ImportFromRootAsync(_importRootPath, CancellationToken.None);
            Snackbar.Add($"Importados {imported} repos desde {_importRootPath}.",
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

    private async Task RemoveRepoAsync(RepoInfo repo)
    {
        await RepoCatalog.RemoveAsync(repo.Path, CancellationToken.None);
        _selectedPaths.Remove(repo.Path);
        Snackbar.Add($"Repo quitado del catálogo: {repo.Name}", Severity.Info);
        await ManualRefresh();
    }

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
