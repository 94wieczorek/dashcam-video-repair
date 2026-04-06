using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DashcamVideoRepair.Models;

public class RepairQueueItem : INotifyPropertyChanged
{
    private FileStatus _status = FileStatus.Pending;
    private double _progress;
    private string? _errorMessage;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    public FileStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public List<RepairResult> RepairAttempts { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
