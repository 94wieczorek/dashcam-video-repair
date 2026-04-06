using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DashcamVideoRepair.Models;
using DashcamVideoRepair.ViewModels;

namespace DashcamVideoRepair;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (DataContext is MainViewModel viewModel && viewModel.IsProcessing)
        {
            var result = MessageBox.Show(
                "Naprawa jest w toku. Anulować i zamknąć?",
                "Potwierdzenie zamknięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                viewModel.CancelRepairs();
            }
            else
            {
                e.Cancel = true;
            }
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;

        var files = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                viewModel.AddFolder(path);
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        if (files.Count > 0)
        {
            viewModel.AddFiles(files);
        }
    }
}
