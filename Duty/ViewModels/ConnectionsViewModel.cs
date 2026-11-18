using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Duty.Models;
using Duty.Services;

namespace Duty.ViewModels;

/// <summary>Строка списка подключений: показывается только название.</summary>
public sealed class ConnectionListItemViewModel
{
    public ConnectionListItemViewModel(
        DbConnectionProfile profile,
        bool isActive,
        Action<ConnectionListItemViewModel> activate,
        Action<ConnectionListItemViewModel> edit)
    {
        Model = profile;
        IsActive = isActive;
        ActivateCommand = new RelayCommand(() => activate(this));
        EditCommand = new RelayCommand(() => edit(this));
    }

    public DbConnectionProfile Model { get; }

    public string Name => Model.Name;

    public bool IsActive { get; }

    public ICommand ActivateCommand { get; }

    public ICommand EditCommand { get; }
}

/// <summary>
/// Список сохранённых подключений и форма их создания/редактирования.
/// Переключение срабатывает сразу по нажатию на строку.
/// </summary>
public partial class ConnectionsViewModel : ObservableObject
{
    private readonly IConnectionProfileService _profiles;
    private readonly IDialogService _dialogs;
    private readonly Func<DbConnectionProfile, Task> _onActivated;
    private Guid _editingId = Guid.Empty;

    public ConnectionsViewModel(
        IConnectionProfileService profiles,
        IDialogService dialogs,
        Func<DbConnectionProfile, Task> onActivated)
    {
        _profiles = profiles;
        _dialogs = dialogs;
        _onActivated = onActivated;

        EditorTitle = "Новое подключение";
        EditorName = string.Empty;
        EditorUrl = string.Empty;
    }

    public ObservableCollection<ConnectionListItemViewModel> Items { get; } = new();

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsEditorOpen { get; set; }

    [ObservableProperty]
    public partial string EditorTitle { get; set; }

    [ObservableProperty]
    public partial string EditorName { get; set; }

    [ObservableProperty]
    public partial string EditorUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditorError))]
    public partial string? EditorError { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorError);

    public bool IsNotBusy => !IsBusy;

    /// <summary>Открывает список подключений и перечитывает его с диска.</summary>
    [RelayCommand]
    private async Task OpenAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await RefreshAsync();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        if (IsBusy)
        {
            return;
        }

        IsOpen = false;
        IsEditorOpen = false;
        EditorError = null;
    }

    private async Task RefreshAsync()
    {
        var profiles = await _profiles.GetProfilesAsync();
        var active = await _profiles.GetActiveProfileAsync();
        var activeId = active?.Id;

        Items.Clear();
        foreach (var profile in profiles)
        {
            Items.Add(new ConnectionListItemViewModel(
                profile,
                profile.Id == activeId,
                item => _ = ActivateAsync(item),
                Edit));
        }
    }

    /// <summary>Мгновенное переключение: сохраняет выбор и перезагружает данные.</summary>
    private async Task ActivateAsync(ConnectionListItemViewModel item)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _profiles.SetActiveAsync(item.Model.Id);
            IsOpen = false;
            IsEditorOpen = false;
            await _onActivated(item.Model);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync("Не удалось переключить подключение", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Add()
    {
        _editingId = Guid.Empty;
        IsEditing = false;
        EditorTitle = "Новое подключение";
        EditorName = string.Empty;
        EditorUrl = string.Empty;
        EditorError = null;
        IsEditorOpen = true;
    }

    private void Edit(ConnectionListItemViewModel item)
    {
        _editingId = item.Model.Id;
        IsEditing = true;
        EditorTitle = "Изменить подключение";
        EditorName = item.Model.Name;
        EditorUrl = item.Model.ConnectionString;
        EditorError = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CloseEditor()
    {
        if (IsBusy)
        {
            return;
        }

        IsEditorOpen = false;
        EditorError = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditorName))
        {
            EditorError = "Укажите название подключения.";
            return;
        }

        if (DbConnectionProfile.NormalizeUrl(EditorUrl) is not string url)
        {
            EditorError = "Укажите адрес вида https://project-default-rtdb.firebaseio.com/";
            return;
        }

        IsBusy = true;
        try
        {
            var profiles = (await _profiles.GetProfilesAsync()).ToList();
            var active = await _profiles.GetActiveProfileAsync();

            var existing = profiles.FirstOrDefault(profile => profile.Id == _editingId);
            if (existing is null)
            {
                existing = new DbConnectionProfile { Id = Guid.NewGuid() };
                profiles.Add(existing);
            }

            existing.Name = EditorName.Trim();
            existing.ConnectionString = url;

            await _profiles.SaveAsync(profiles, active?.Id ?? existing.Id);
            await RefreshAsync();

            IsEditorOpen = false;
            IsOpen = true;
            EditorError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DataStorageException)
        {
            EditorError = $"Не удалось сохранить: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy || !IsEditing)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Удалить подключение?",
            "Данные в облачной базе останутся, но подключение исчезнет из списка.",
            "Удалить",
            "Отмена");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var profiles = (await _profiles.GetProfilesAsync()).ToList();
            var target = profiles.FirstOrDefault(profile => profile.Id == _editingId);
            if (target is null)
            {
                EditorError = "Подключение не найдено.";
                return;
            }

            profiles.Remove(target);
            var active = await _profiles.GetActiveProfileAsync();
            var nextActive = active?.Id == target.Id ? profiles.FirstOrDefault()?.Id : active?.Id;

            await _profiles.SaveAsync(profiles, nextActive);
            await RefreshAsync();

            IsEditorOpen = false;
            IsOpen = true;
            EditorError = null;
        }
        catch (Exception ex)
        {
            EditorError = $"Не удалось удалить: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
