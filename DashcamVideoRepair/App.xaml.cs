using System.Windows;
using DashcamVideoRepair.Infrastructure;
using DashcamVideoRepair.Services;
using DashcamVideoRepair.ViewModels;
using DashcamVideoRepair.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;

namespace DashcamVideoRepair;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Initialize Serilog
        var logger = LoggingConfig.CreateLogger();
        logger.Information("Application starting");

        // 2. Create a temporary ConfigStore for tool discovery
        var configStore = new ConfigStore(logger);
        var discoveryService = new ToolDiscoveryService(configStore, logger);

        // 3. Discover ffmpeg
        var ffmpegPath = await discoveryService.DiscoverFfmpegAsync();

        if (string.IsNullOrEmpty(ffmpegPath))
        {
            var dialog = new OpenFileDialog
            {
                Title = "Wybierz ffmpeg.exe",
                Filter = "Plik wykonywalny ffmpeg (ffmpeg.exe)|ffmpeg.exe|Wszystkie pliki (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true && discoveryService.ValidateFfmpegPath(dialog.FileName))
            {
                ffmpegPath = dialog.FileName;
                var config = await configStore.LoadAsync();
                config.FfmpegPath = ffmpegPath;
                await configStore.SaveAsync(config);
                logger.Information("User selected ffmpeg path: {Path}", ffmpegPath);
            }
            else
            {
                logger.Error("ffmpeg.exe not found and user did not provide a valid path");
                MessageBox.Show(
                    "ffmpeg.exe jest wymagany do działania tej aplikacji.\nUpewnij się, że ffmpeg.exe i ffprobe.exe są dostępne.",
                    "Nie znaleziono FFmpeg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
        }

        // 4. Load config for untrunc path
        var appConfig = await configStore.LoadAsync();

        // 5. Build DI container
        var services = new ServiceCollection();

        services.AddSingleton<ILogger>(logger);
        services.AddSingleton<IConfigStore>(configStore);
        services.AddSingleton<IFileValidator, FileValidator>();
        services.AddSingleton<IToolDiscoveryService, ToolDiscoveryService>();

        services.AddSingleton<IFfmpegProcess>(sp =>
            new FfmpegProcess(ffmpegPath!, sp.GetRequiredService<ILogger>()));

        services.AddSingleton<IUntruncProcess>(sp =>
        {
            // Use a factory that reads config dynamically so settings changes take effect
            return new DynamicUntruncProcess(
                sp.GetRequiredService<IConfigStore>(),
                sp.GetRequiredService<ILogger>());
        });

        services.AddSingleton<IOutputValidator, OutputValidator>();
        services.AddSingleton<VideoRepairService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // 6. Create MainWindow with wired commands
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

        mainViewModel.SetSelectFilesAction(async () =>
        {
            var config = await configStore.LoadAsync();
            var fileDialog = new OpenFileDialog
            {
                Title = "Wybierz pliki wideo do naprawy",
                Filter = "Pliki wideo (*.mov;*.mp4)|*.mov;*.mp4|Wszystkie pliki (*.*)|*.*",
                Multiselect = true
            };

            if (!string.IsNullOrEmpty(config.LastUsedFolder)
                && System.IO.Directory.Exists(config.LastUsedFolder))
            {
                fileDialog.InitialDirectory = config.LastUsedFolder;
            }

            if (fileDialog.ShowDialog() == true)
            {
                mainViewModel.AddFiles(fileDialog.FileNames);
                var lastFolder = System.IO.Path.GetDirectoryName(fileDialog.FileNames[0]);
                if (!string.IsNullOrEmpty(lastFolder))
                {
                    config.LastUsedFolder = lastFolder;
                    await configStore.SaveAsync(config);
                }
            }
        });

        mainViewModel.SetOpenSettingsAction(() =>
        {
            var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
            var settingsWindow = new SettingsWindow(settingsVm)
            {
                Owner = Current.MainWindow
            };
            settingsWindow.ShowDialog();
        });

        var mainWindow = new MainWindow(mainViewModel);
        mainWindow.Show();

        logger.Information("Application started successfully");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
