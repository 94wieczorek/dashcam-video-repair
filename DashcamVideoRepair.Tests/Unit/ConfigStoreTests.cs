using System.Text.Json;
using DashcamVideoRepair.Infrastructure;
using DashcamVideoRepair.Models;
using FluentAssertions;
using Serilog;

namespace DashcamVideoRepair.Tests.Unit;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILogger _logger;

    public ConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _logger = new LoggerConfiguration().CreateLogger();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private ConfigStore CreateStore(string fileName = "config.json")
    {
        var path = Path.Combine(_tempDir, fileName);
        return new ConfigStore(path, _logger);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsDefaults()
    {
        var store = CreateStore();

        var config = await store.LoadAsync();

        config.Should().NotBeNull();
        config.FfmpegPath.Should().BeNull();
        config.UntruncPath.Should().BeNull();
        config.ReferenceFilePath.Should().BeNull();
        config.LastUsedFolder.Should().BeNull();
        config.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTrips()
    {
        var store = CreateStore();
        var original = new AppConfig
        {
            FfmpegPath = @"C:\tools\ffmpeg.exe",
            UntruncPath = @"C:\tools\untrunc.exe",
            ReferenceFilePath = @"D:\ref.mp4",
            LastUsedFolder = @"D:\Videos",
            TimeoutSeconds = 600
        };

        await store.SaveAsync(original);
        var loaded = await store.LoadAsync();

        loaded.FfmpegPath.Should().Be(original.FfmpegPath);
        loaded.UntruncPath.Should().Be(original.UntruncPath);
        loaded.ReferenceFilePath.Should().Be(original.ReferenceFilePath);
        loaded.LastUsedFolder.Should().Be(original.LastUsedFolder);
        loaded.TimeoutSeconds.Should().Be(original.TimeoutSeconds);
    }

    [Fact]
    public async Task LoadAsync_CorruptJson_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "config.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json!!!");
        var store = new ConfigStore(path, _logger);

        var config = await store.LoadAsync();

        config.Should().NotBeNull();
        config.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var store = new ConfigStore(path, _logger);

        await store.SaveAsync(new AppConfig());

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        var store = CreateStore();

        await store.SaveAsync(new AppConfig { FfmpegPath = @"C:\tools\first.exe" });
        await store.SaveAsync(new AppConfig { FfmpegPath = @"C:\tools\second.exe" });
        var loaded = await store.LoadAsync();

        loaded.FfmpegPath.Should().Be(@"C:\tools\second.exe");
    }

    [Fact]
    public async Task LoadAsync_NullDeserializationResult_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "config.json");
        await File.WriteAllTextAsync(path, "null");
        var store = new ConfigStore(path, _logger);

        var config = await store.LoadAsync();

        config.Should().NotBeNull();
        config.TimeoutSeconds.Should().Be(300);
    }
}
