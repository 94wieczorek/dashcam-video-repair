using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DashcamVideoRepair.Infrastructure;
using DashcamVideoRepair.Models;
using DashcamVideoRepair.Services;
using Serilog;

namespace DashcamVideoRepair.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mov", ".mp4" };

    private readonly VideoRepairService _repairService;
    private readonly IConfigStore _configStore;
    private readonly ILogger _logger;

    private bool _isProcessing;
    private string? _batchSummary;
    private string? _warningMessage;
    private string? _lastOutputDirectory;
    private CancellationTokenSource? _cancellationTokenSource;

    public MainViewModel(VideoRepairService repairService, IConfigStore configStore, ILogger logger)
    {
        _repairService = repairService ?? throw new ArgumentNullException(nameof(repairService));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RepairQueue = new ObservableCollection<RepairQueueItem>();

        StartRepairCommand = new RelayCommand(async _ => await StartRepairAsync(), _ => !IsProcessing && RepairQueue.Any(i => i.Status == FileStatus.Pending));
        SelectFilesCommand = new RelayCommand(_ => _selectFilesAction?.Invoke(), _ => !IsProcessing);
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder(), _ => _lastOutputDirectory != null);
        OpenSettingsCommand = new RelayCommand(_ => _openSettingsAction?.Invoke());
    }

    private Action? _selectFilesAction;
    private Action? _openSettingsAction;

    /// <summary>
    /// Sets the action invoked when the user clicks "Select Files".
    /// </summary>
    public void SetSelectFilesAction(Action action) => _selectFilesAction = action;

    /// <summary>
    /// Sets the action invoked when the user clicks "Settings".
    /// </summary>
    public void SetOpenSettingsAction(Action action) => _openSettingsAction = action;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RepairQueueItem> RepairQueue { get; }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (SetField(ref _isProcessing, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public string? BatchSummary
    {
        get => _batchSummary;
        set => SetField(ref _batchSummary, value);
    }

    public string? WarningMessage
    {
        get => _warningMessage;
        set => SetField(ref _warningMessage, value);
    }

    public ICommand StartRepairCommand { get; }
    public ICommand SelectFilesCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    /// <summary>
    /// Adds files to the repair queue, filtering for .mov/.mp4, rejecting unsupported, and skipping duplicates.
    /// </summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        var unsupported = new List<string>();
        var duplicates = new List<string>();
        var added = 0;

        foreach (var path in paths)
        {
            var ext = Path.GetExtension(path);
            if (!SupportedExtensions.Contains(ext))
            {
                unsupported.Add(Path.GetFileName(path));
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (RepairQueue.Any(item => string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                duplicates.Add(Path.GetFileName(path));
                continue;
            }

            RepairQueue.Add(new RepairQueueItem
            {
                FilePath = fullPath,
                FileName = Path.GetFileName(path),
                Status = FileStatus.Pending
            });
            added++;
        }

        BuildWarningMessage(unsupported, duplicates);
        _logger.Information("Added {Count} files to repair queue", added);
    }

    /// <summary>
    /// Recursively scans a folder for .mov/.mp4 files and adds them to the queue.
    /// </summary>
    public void AddFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            WarningMessage = $"Nie znaleziono folderu: {folderPath}";
            return;
        }

        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

        AddFiles(files);
    }

    public void SetLastOutputDirectory(string? directory)
    {
        _lastOutputDirectory = directory;
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Cancels any in-progress repair operations.
    /// </summary>
    public void CancelRepairs()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task StartRepairAsync()
    {
        if (IsProcessing)
            return;

        IsProcessing = true;
        BatchSummary = null;
        WarningMessage = null;
        _cancellationTokenSource = new CancellationTokenSource();

        var ct = _cancellationTokenSource.Token;
        var successCount = 0;
        var failedCount = 0;

        _logger.Information("Starting batch repair for {Count} pending files",
            RepairQueue.Count(i => i.Status == FileStatus.Pending));

        try
        {
            var pendingItems = RepairQueue.Where(i => i.Status == FileStatus.Pending).ToList();

            foreach (var item in pendingItems)
            {
                if (ct.IsCancellationRequested)
                    break;

                var progress = new Progress<double>(p => item.Progress = p);

                try
                {
                    await _repairService.ProcessFileAsync(item, progress, ct);
                }
                catch (OperationCanceledException)
                {
                    _logger.Information("Repair cancelled for {FilePath}", item.FilePath);
                    if (item.Status == FileStatus.Processing)
                    {
                        item.Status = FileStatus.Failed;
                        item.ErrorMessage = "Naprawa anulowana przez użytkownika.";
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error processing {FilePath}", item.FilePath);
                    item.Status = FileStatus.Failed;
                    item.ErrorMessage = $"Nieoczekiwany błąd: {ex.Message}";
                }

                if (item.Status == FileStatus.Success)
                {
                    successCount++;
                    var dir = Path.GetDirectoryName(item.FilePath);
                    if (!string.IsNullOrEmpty(dir))
                        _lastOutputDirectory = dir;
                }
                else if (item.Status == FileStatus.Failed)
                {
                    failedCount++;
                }
            }
        }
        finally
        {
            IsProcessing = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            BatchSummary = $"{successCount} naprawionych, {failedCount} nieudanych";
            _logger.Information("Batch repair complete: {Summary}", BatchSummary);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OpenOutputFolder()
    {
        if (_lastOutputDirectory != null && Directory.Exists(_lastOutputDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lastOutputDirectory,
                UseShellExecute = true
            });
        }
    }

    private void BuildWarningMessage(List<string> unsupported, List<string> duplicates)
    {
        var parts = new List<string>();

        if (unsupported.Count > 0)
            parts.Add($"Odrzucone nieobsługiwane pliki: {string.Join(", ", unsupported)}");

        if (duplicates.Count > 0)
            parts.Add($"Pominięte duplikaty: {string.Join(", ", duplicates)}");

        WarningMessage = parts.Count > 0 ? string.Join(". ", parts) : null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
