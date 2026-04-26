using DevHub.Services.SecretProfiles;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Components.Pages;

public partial class SecretProfiles
{
    private IReadOnlyList<ServiceProfileView> _services = [];
    private readonly Dictionary<string, ServiceState> _state = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        _services = await ProfileService.GetServicesAsync(CancellationToken.None);
        _state.Clear();
        foreach (var svc in _services)
        {
            _state[svc.Name] = await LoadStateAsync(svc.Name);
        }
    }

    private async Task<ServiceState> LoadStateAsync(string serviceName)
    {
        var profiles = await ProfileService.GetProfilesAsync(serviceName, CancellationToken.None);
        var active = await ProfileService.GetActiveProfileAsync(serviceName, CancellationToken.None);
        return new ServiceState(profiles, active);
    }

    private async Task ReloadServiceAsync(string serviceName)
    {
        _state[serviceName] = await LoadStateAsync(serviceName);
        StateHasChanged();
    }

    private async Task CaptureAsync(string serviceName)
    {
        var parameters = new DialogParameters<PromptDialog>
        {
            { x => x.Title, "Capturar perfil" },
            { x => x.Label, "Nombre del perfil" },
            { x => x.HelperText, "Solo A-Z a-z 0-9 . _ -" },
        };
        var dlg = await DialogService.ShowAsync<PromptDialog>("Capturar", parameters);
        var result = await dlg.Result;
        if (result is null || result.Canceled)
        {
            return;
        }
        var name = (string?)result.Data;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await ProfileService.CaptureAsync(serviceName, name, CancellationToken.None);
            Snackbar.Add($"Perfil '{name}' capturado.", Severity.Success);
            await ReloadServiceAsync(serviceName);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task ApplyAsync(string serviceName, ProfileInfo profile)
    {
        bool prodConfirmed = false;
        if (profile.IsProd)
        {
            var parameters = new DialogParameters<ProdConfirmDialog>
            {
                { x => x.ServiceName, serviceName },
                { x => x.ProfileName, profile.Name },
            };
            var dlg = await DialogService.ShowAsync<ProdConfirmDialog>("Confirmar aplicar PROD", parameters,
                new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = false });
            var result = await dlg.Result;
            if (result is null || result.Canceled)
            {
                return;
            }
            prodConfirmed = true;
        }

        try
        {
            await ProfileService.ApplyAsync(serviceName, profile.Name, prodConfirmed, CancellationToken.None);
            Snackbar.Add($"Aplicado '{profile.Name}' a {serviceName}.", profile.IsProd ? Severity.Warning : Severity.Success);
            await ReloadServiceAsync(serviceName);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task DeleteAsync(string serviceName, string profileName)
    {
        var ok = await DialogService.ShowMessageBoxAsync(new MessageBoxOptions
        {
            Title = "Borrar perfil",
            Message = $"¿Borrar el perfil '{profileName}' de {serviceName}? No se puede deshacer.",
            YesText = "Borrar",
            CancelText = "Cancelar",
        });
        if (ok != true)
        {
            return;
        }

        try
        {
            await ProfileService.DeleteAsync(serviceName, profileName, CancellationToken.None);
            Snackbar.Add($"Perfil '{profileName}' borrado.", Severity.Info);
            await ReloadServiceAsync(serviceName);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task ViewProfileAsync(string serviceName, string profileName)
    {
        try
        {
            var content = await ProfileService.ReadProfileContentAsync(serviceName, profileName, CancellationToken.None);
            var dlg = await DialogService.ShowAsync<JsonViewerDialog>(
                $"{serviceName} · {profileName}.json",
                new DialogParameters<JsonViewerDialog>
                {
                    { x => x.Content, content },
                    { x => x.ServiceName, serviceName },
                    { x => x.ProfileName, profileName },
                },
                new DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true });
            await dlg.Result;
            await ReloadServiceAsync(serviceName);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task ViewLiveAsync(string serviceName)
    {
        try
        {
            var content = await ProfileService.ReadActiveContentAsync(serviceName, CancellationToken.None);
            await DialogService.ShowAsync<JsonViewerDialog>(
                $"{serviceName} · secrets.json (live)",
                new DialogParameters<JsonViewerDialog> { { x => x.Content, content } },
                new DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true });
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task DiffProfileAsync(string serviceName, string profileName)
    {
        try
        {
            var original = await ProfileService.ReadActiveContentAsync(serviceName, CancellationToken.None);
            var modified = await ProfileService.ReadProfileContentAsync(serviceName, profileName, CancellationToken.None);
            await DialogService.ShowAsync<JsonDiffDialog>(
                $"Diff · live ↔ {profileName}.json",
                new DialogParameters<JsonDiffDialog>
                {
                    { x => x.Original, original },
                    { x => x.Modified, modified },
                },
                new DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true });
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private static string FormatSize(long bytes) => bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024.0:F1} KB";

    private sealed record ServiceState(IReadOnlyList<ProfileInfo> Profiles, ActiveProfileInfo Active);
}
