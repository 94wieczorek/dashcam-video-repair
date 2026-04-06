using System.Windows;
using DashcamVideoRepair.ViewModels;

namespace DashcamVideoRepair.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
