using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DashcamVideoRepair.Infrastructure;
using DashcamVideoRepair.Models;
using Microsoft.Win32;

namespace DashcamVideoRepair.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IConfigStore _configStore;
    private string? _untruncPath;
    private string? _referenceFilePath;

    public SettingsViewModel(IConfigStore configStore)
    {
        _configStore = configStore;
        BrowseUntruncCommand = new RelayCommand(BrowseUntrunc);
        BrowseReferenceFileCommand = new RelayCommand(BrowseReferenceFile);
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        _ = LoadAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<bool>? CloseRequested;

    public string? UntruncPath
    {
        get => _untruncPath;
        set { _untruncPath = value; OnPropertyChanged(); }
    }

    public string? ReferenceFilePath
    {
        get => _referenceFilePath;
        set { _referenceFilePath = value; OnPropertyChanged(); }
    }

    public ICommand BrowseUntruncCommand { get; }
    public ICommand BrowseReferenceFileCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task LoadAsync()
    {
        var config = await _configStore.LoadAsync();
        UntruncPath = config.UntruncPath;
        ReferenceFilePath = config.ReferenceFilePath;
    }

    private async Task SaveAsync()
    {
        var config = await _configStore.LoadAsync();
        config.UntruncPath = UntruncPath;
        config.ReferenceFilePath = ReferenceFilePath;
        await _configStore.SaveAsync(config);
        CloseRequested?.Invoke(true);
    }

    private void BrowseUntrunc(object? parameter)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz plik wykonywalny untrunc",
            Filter = "Pliki wykonywalne (*.exe)|*.exe|Wszystkie pliki (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            UntruncPath = dialog.FileName;
        }
    }

    private void BrowseReferenceFile(object? parameter)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz referencyjny plik wideo",
            Filter = "Pliki wideo (*.mov;*.mp4)|*.mov;*.mp4|Wszystkie pliki (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            ReferenceFilePath = dialog.FileName;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}
