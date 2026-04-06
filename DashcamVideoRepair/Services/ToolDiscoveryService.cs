using System.IO;
using DashcamVideoRepair.Infrastructure;
using Serilog;

namespace DashcamVideoRepair.Services;

public class ToolDiscoveryService : IToolDiscoveryService
{
    private readonly IConfigStore _configStore;
    private readonly ILogger _logger;

    public ToolDiscoveryService(IConfigStore configStore, ILogger logger)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> DiscoverFfmpegAsync()
    {
        // 1. Check ConfigStore for a previously saved path
        var config = await _configStore.LoadAsync();
        if (!string.IsNullOrWhiteSpace(config.FfmpegPath) && ValidateFfmpegPath(config.FfmpegPath))
        {
            _logger.Information("Using previously configured ffmpeg path: {Path}", config.FfmpegPath);
            return config.FfmpegPath;
        }

        // 2. Check the application directory
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var appDirPath = Path.Combine(appDir, "ffmpeg.exe");
        if (ValidateFfmpegPath(appDirPath))
        {
            _logger.Information("Found ffmpeg in application directory: {Path}", appDirPath);
            await SaveDiscoveredPathAsync(appDirPath);
            return appDirPath;
        }

        // 3. Check the system PATH
        var pathResult = FindOnSystemPath("ffmpeg.exe");
        if (pathResult != null && ValidateFfmpegPath(pathResult))
        {
            _logger.Information("Found ffmpeg on system PATH: {Path}", pathResult);
            await SaveDiscoveredPathAsync(pathResult);
            return pathResult;
        }

        _logger.Warning("ffmpeg.exe was not found in application directory or system PATH");
        return null;
    }

    public bool ValidateFfmpegPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!File.Exists(path))
        {
            _logger.Debug("ffmpeg not found at {Path}", path);
            return false;
        }

        // Check that ffprobe.exe is co-located in the same directory
        var directory = Path.GetDirectoryName(path);
        if (directory == null)
            return false;

        var ffprobePath = Path.Combine(directory, "ffprobe.exe");
        if (!File.Exists(ffprobePath))
        {
            _logger.Warning("ffprobe.exe not found co-located with ffmpeg at {Directory}", directory);
            return false;
        }

        return true;
    }

    private async Task SaveDiscoveredPathAsync(string ffmpegPath)
    {
        var config = await _configStore.LoadAsync();
        config.FfmpegPath = ffmpegPath;
        await _configStore.SaveAsync(config);
        _logger.Information("Saved discovered ffmpeg path to config: {Path}", ffmpegPath);
    }

    internal static string? FindOnSystemPath(string executable)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        var directories = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in directories)
        {
            var fullPath = Path.Combine(dir.Trim(), executable);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
