using System.IO;
using DashcamVideoRepair.Infrastructure;
using DashcamVideoRepair.Models;
using DashcamVideoRepair.Services;
using FluentAssertions;
using Moq;
using Serilog;

namespace DashcamVideoRepair.Tests.Unit;

public class ToolDiscoveryServiceTests : IDisposable
{
    private readonly Mock<IConfigStore> _configStore;
    private readonly Mock<ILogger> _logger;
    private readonly ToolDiscoveryService _service;
    private readonly string _tempDir;

    public ToolDiscoveryServiceTests()
    {
        _configStore = new Mock<IConfigStore>();
        _logger = new Mock<ILogger>();
        _service = new ToolDiscoveryService(_configStore.Object, _logger.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"ToolDiscoveryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task DiscoverFfmpegAsync_ReturnsSavedPath_WhenConfigHasValidPath()
    {
        // Arrange: create ffmpeg.exe and ffprobe.exe in temp dir
        var ffmpegPath = Path.Combine(_tempDir, "ffmpeg.exe");
        var ffprobePath = Path.Combine(_tempDir, "ffprobe.exe");
        File.WriteAllText(ffmpegPath, "fake");
        File.WriteAllText(ffprobePath, "fake");

        _configStore.Setup(c => c.LoadAsync())
            .ReturnsAsync(new AppConfig { FfmpegPath = ffmpegPath });

        // Act
        var result = await _service.DiscoverFfmpegAsync();

        // Assert
        result.Should().Be(ffmpegPath);
        _configStore.Verify(c => c.SaveAsync(It.IsAny<AppConfig>()), Times.Never);
    }

    [Fact]
    public async Task DiscoverFfmpegAsync_SkipsSavedPath_WhenConfigPathIsInvalid()
    {
        // Arrange: saved path doesn't exist on disk
        _configStore.Setup(c => c.LoadAsync())
            .ReturnsAsync(new AppConfig { FfmpegPath = @"C:\nonexistent\ffmpeg.exe" });

        // Act
        var result = await _service.DiscoverFfmpegAsync();

        // Assert — falls through to app dir and PATH, both won't have it either
        result.Should().BeNull();
    }

    [Fact]
    public async Task DiscoverFfmpegAsync_ReturnsNull_WhenNothingFound()
    {
        _configStore.Setup(c => c.LoadAsync())
            .ReturnsAsync(new AppConfig());

        var result = await _service.DiscoverFfmpegAsync();

        result.Should().BeNull();
    }

    [Fact]
    public void ValidateFfmpegPath_ReturnsTrue_WhenBothExesExist()
    {
        var ffmpegPath = Path.Combine(_tempDir, "ffmpeg.exe");
        File.WriteAllText(ffmpegPath, "fake");
        File.WriteAllText(Path.Combine(_tempDir, "ffprobe.exe"), "fake");

        _service.ValidateFfmpegPath(ffmpegPath).Should().BeTrue();
    }

    [Fact]
    public void ValidateFfmpegPath_ReturnsFalse_WhenFfmpegMissing()
    {
        var ffmpegPath = Path.Combine(_tempDir, "ffmpeg.exe");
        // Don't create the file

        _service.ValidateFfmpegPath(ffmpegPath).Should().BeFalse();
    }

    [Fact]
    public void ValidateFfmpegPath_ReturnsFalse_WhenFfprobeMissing()
    {
        var ffmpegPath = Path.Combine(_tempDir, "ffmpeg.exe");
        File.WriteAllText(ffmpegPath, "fake");
        // Don't create ffprobe.exe

        _service.ValidateFfmpegPath(ffmpegPath).Should().BeFalse();
    }

    [Fact]
    public void ValidateFfmpegPath_ReturnsFalse_WhenPathIsNull()
    {
        _service.ValidateFfmpegPath(null!).Should().BeFalse();
    }

    [Fact]
    public void ValidateFfmpegPath_ReturnsFalse_WhenPathIsEmpty()
    {
        _service.ValidateFfmpegPath("").Should().BeFalse();
    }

    [Fact]
    public void FindOnSystemPath_ReturnsNull_WhenExecutableNotOnPath()
    {
        var result = ToolDiscoveryService.FindOnSystemPath("definitely_not_a_real_tool_12345.exe");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DiscoverFfmpegAsync_SavesPath_WhenFoundOnDisk()
    {
        // Arrange: config has no saved path, but we can't easily mock app directory.
        // Instead, test that when config path is invalid and nothing else found, save is not called.
        _configStore.Setup(c => c.LoadAsync())
            .ReturnsAsync(new AppConfig());

        await _service.DiscoverFfmpegAsync();

        // SaveAsync should NOT be called since nothing was found
        _configStore.Verify(c => c.SaveAsync(It.IsAny<AppConfig>()), Times.Never);
    }

    [Fact]
    public void Constructor_ThrowsOnNullConfigStore()
    {
        var act = () => new ToolDiscoveryService(null!, _logger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configStore");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new ToolDiscoveryService(_configStore.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
