using System.IO;
using Serilog;

namespace DashcamVideoRepair.Infrastructure;

public class FileValidator : IFileValidator
{
    private const long MinimumFileSizeBytes = 1_048_576; // 1 MB

    private readonly ILogger _logger;

    public FileValidator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<(bool IsValid, string? FailureReason)> ValidateAsync(string filePath)
    {
        // Check file exists
        if (!File.Exists(filePath))
        {
            _logger.Warning("File not found: {FilePath}", filePath);
            return Task.FromResult<(bool, string?)>((false, "Nie znaleziono pliku pod podaną ścieżką"));
        }

        // Check file size >= 1 MB
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < MinimumFileSizeBytes)
        {
            _logger.Warning("File too small ({Size} bytes): {FilePath}", fileInfo.Length, filePath);
            return Task.FromResult<(bool, string?)>((false, "Plik jest za mały, aby zawierać prawidłowe dane wideo (mniej niż 1 MB)"));
        }

        // Check file is not locked
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException)
        {
            _logger.Warning("File is locked or inaccessible: {FilePath}", filePath);
            return Task.FromResult<(bool, string?)>((false, "Plik jest niedostępny lub używany przez inny proces"));
        }

        _logger.Information("File validation passed: {FilePath}", filePath);
        return Task.FromResult<(bool, string?)>((true, null));
    }
}
