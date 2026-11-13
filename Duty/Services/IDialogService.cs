namespace Duty.Services;

/// <summary>Абстракция диалогов, чтобы ViewModel не зависела от Page.</summary>
public interface IDialogService
{
    Task ShowAsync(string title, string message, string cancel = "OK");

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
