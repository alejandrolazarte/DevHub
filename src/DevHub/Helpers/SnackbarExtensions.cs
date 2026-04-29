using MudBlazor;

namespace DevHub.Helpers;

internal static class SnackbarExtensions
{
    internal static async Task<bool> TryAsync(
        this ISnackbar snackbar,
        Func<Task> operation,
        string? successMessage = null,
        Severity successSeverity = Severity.Success)
    {
        try
        {
            await operation();
            if (successMessage is not null)
            {
                snackbar.Add(successMessage, successSeverity);
            }
            return true;
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            return false;
        }
    }
}
