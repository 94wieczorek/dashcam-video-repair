using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DashcamVideoRepair.Models;

namespace DashcamVideoRepair;

/// <summary>
/// Converts FileStatus to a SolidColorBrush for the status indicator ellipse.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileStatus status)
        {
            return status switch
            {
                FileStatus.Success => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),   // Green
                FileStatus.Failed => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),    // Red
                FileStatus.Processing => new SolidColorBrush(Color.FromRgb(0xFF, 0xCA, 0x28)), // Yellow
                _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),                     // Gray (Pending)
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts FileStatus to Visibility — visible only when Processing.
/// Used for the progress bar.
/// </summary>
public class StatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileStatus status && status == FileStatus.Processing)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a nullable value to Visibility — Visible when non-null/non-empty, Collapsed otherwise.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        return value is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Inverts a boolean value. Used to disable DropZone when IsProcessing is true.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}
