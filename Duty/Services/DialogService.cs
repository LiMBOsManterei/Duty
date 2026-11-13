using Microsoft.Maui.ApplicationModel;

namespace Duty.Services;

/// <summary>Показывает системные диалоги поверх текущей страницы.</summary>
public sealed class DialogService : IDialogService
{
    public Task ShowAsync(string title, string message, string cancel = "OK") =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = GetCurrentPage();
            if (page is not null)
            {
                await page.DisplayAlertAsync(title, message, cancel);
            }
        });

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = GetCurrentPage();
            return page is not null && await page.DisplayAlertAsync(title, message, accept, cancel);
        });

    private static Page? GetCurrentPage() =>
        Application.Current?.Windows.FirstOrDefault()?.Page;
}
