using System.IO;
using System.Text.Json;
using DashcamVideoRepair.Models;
using Serilog;

namespace DashcamVideoRepair.Infrastructure;

public class ConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configFilePath;
    private readonly ILogger _logger;

    public ConfigStore(ILogger logger)
        : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), logger)
    {
    }

    public ConfigStore(string configFilePath, ILogger logger)
    {
        _configFilePath = configFilePath;
        _logger = logger;
    }

    public async Task<AppConfig> LoadAsync()
    {
        if (!File.Exists(_configFilePath))
        {
            _logger.Information("Config file not found at {Path}, returning defaults", _configFilePath);
            return new AppConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config == null)
                return new AppConfig();

            // Resolve relative paths against the application directory
            var appDir = Path.GetDirectoryName(_configFilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            config.FfmpegPath = ResolvePath(config.FfmpegPath, appDir);
            config.UntruncPath = ResolvePath(config.UntruncPath, appDir);
            config.ReferenceFilePath = ResolvePath(config.ReferenceFilePath, appDir);

            return config;
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Corrupt config file at {Path}, returning defaults", _configFilePath);
            return new AppConfig();
        }
    }

    private static string? ResolvePath(string? path, string baseDir)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(Path.Combine(baseDir, path));
    }

    public async Task SaveAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var directory = Path.GetDirectoryName(_configFilePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_configFilePath, json);
    }
}
