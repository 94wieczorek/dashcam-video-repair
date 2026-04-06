using DashcamVideoRepair.Infrastructure;
using Serilog;

namespace DashcamVideoRepair.Tests.Unit;

public class FileValidatorTests : IDisposable
{
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();
    private readonly FileValidator _validator;
    private readonly string _tempDir;

    public FileValidatorTests()
    {
        _validator = new FileValidator(_logger);
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileValidatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task ValidateAsync_FileDoesNotExist_ReturnsInvalidWithNotFoundReason()
    {
        var path = Path.Combine(_tempDir, "nonexistent.mp4");

        var (isValid, reason) = await _validator.ValidateAsync(path);

        Assert.False(isValid);
        Assert.Equal("Nie znaleziono pliku pod podaną ścieżką", reason);
    }

    [Fact]
    public async Task ValidateAsync_FileSmallerThan1MB_ReturnsInvalidWithTooSmallReason()
    {
        var path = Path.Combine(_tempDir, "small.mp4");
        await File.WriteAllBytesAsync(path, new byte[1_000_000]); // just under 1 MB

        var (isValid, reason) = await _validator.ValidateAsync(path);

        Assert.False(isValid);
        Assert.Equal("Plik jest za mały, aby zawierać prawidłowe dane wideo (mniej niż 1 MB)", reason);
    }

    [Fact]
    public async Task ValidateAsync_FileExactly1MB_ReturnsValid()
    {
        var path = Path.Combine(_tempDir, "exact.mp4");
        await File.WriteAllBytesAsync(path, new byte[1_048_576]); // exactly 1 MB

        var (isValid, reason) = await _validator.ValidateAsync(path);

        Assert.True(isValid);
        Assert.Null(reason);
    }

    [Fact]
    public async Task ValidateAsync_FileLargerThan1MB_ReturnsValid()
    {
        var path = Path.Combine(_tempDir, "large.mp4");
        await File.WriteAllBytesAsync(path, new byte[2_000_000]);

        var (isValid, reason) = await _validator.ValidateAsync(path);

        Assert.True(isValid);
        Assert.Null(reason);
    }

    [Fact]
    public async Task ValidateAsync_FileIsLocked_ReturnsInvalidWithLockedReason()
    {
        var path = Path.Combine(_tempDir, "locked.mp4");
        await File.WriteAllBytesAsync(path, new byte[2_000_000]);

        // Lock the file by holding an exclusive handle
        using var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var (isValid, reason) = await _validator.ValidateAsync(path);

        Assert.False(isValid);
        Assert.Equal("Plik jest niedostępny lub używany przez inny proces", reason);
    }

    [Fact]
    public async Task ValidateAsync_EmptyFile_ReturnsInvalidWithTooSmallReason()
    {
        var path = Path.Combine(_tempDir, "empty.mp4");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());

        var (isValid, reason) = await _validator.ValidateAsync(path);

        Assert.False(isValid);
        Assert.Equal("Plik jest za mały, aby zawierać prawidłowe dane wideo (mniej niż 1 MB)", reason);
    }
}
