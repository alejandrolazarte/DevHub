using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DevHub.Helpers;

internal static class DialogServiceExtensions
{
    internal static async Task<bool> ShowAndWaitAsync<TDialog>(
        this IDialogService service,
        string title,
        DialogParameters<TDialog> parameters,
        DialogOptions? options = null)
        where TDialog : ComponentBase
    {
        var dlg = options is null
            ? await service.ShowAsync<TDialog>(title, parameters)
            : await service.ShowAsync<TDialog>(title, parameters, options);
        var result = await dlg.Result;
        return result is { Canceled: false };
    }

    internal static async Task<T?> ShowAndGetAsync<TDialog, T>(
        this IDialogService service,
        string title,
        DialogParameters<TDialog> parameters,
        DialogOptions? options = null)
        where TDialog : ComponentBase
    {
        var dlg = options is null
            ? await service.ShowAsync<TDialog>(title, parameters)
            : await service.ShowAsync<TDialog>(title, parameters, options);
        var result = await dlg.Result;
        return result is { Canceled: false, Data: T data } ? data : default;
    }
}
