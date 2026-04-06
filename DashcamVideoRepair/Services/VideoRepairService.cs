using System.IO;
using DashcamVideoRepair.Infrastructure;
using DashcamVideoRepair.Models;
using Serilog;

namespace DashcamVideoRepair.Services;

public class VideoRepairService
{
    private readonly IFfmpegProcess _ffmpegProcess;
    private readonly IUntruncProcess _untruncProcess;
    private readonly IOutputValidator _outputValidator;
    private readonly IFileValidator _fileValidator;
    private readonly IConfigStore _configStore;
    private readonly ILogger _logger;

    public VideoRepairService(
        IFfmpegProcess ffmpegProcess,
        IUntruncProcess untruncProcess,
        IOutputValidator outputValidator,
        IFileValidator fileValidator,
        IConfigStore configStore,
        ILogger logger)
    {
        _ffmpegProcess = ffmpegProcess ?? throw new ArgumentNullException(nameof(ffmpegProcess));
        _untruncProcess = untruncProcess ?? throw new ArgumentNullException(nameof(untruncProcess));
        _outputValidator = outputValidator ?? throw new ArgumentNullException(nameof(outputValidator));
        _fileValidator = fileValidator ?? throw new ArgumentNullException(nameof(fileValidator));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessFileAsync(RepairQueueItem item, IProgress<double>? progress, CancellationToken ct)
    {
        item.Status = FileStatus.Processing;
        item.RepairAttempts.Clear();

        var config = await _configStore.LoadAsync();
        var timeoutSeconds = config.TimeoutSeconds;

        // Validate file before repair
        var (isValid, failureReason) = await _fileValidator.ValidateAsync(item.FilePath);
        if (!isValid)
        {
            item.Status = FileStatus.Failed;
            item.ErrorMessage = failureReason;
            _logger.Warning("File validation failed for {FilePath}: {Reason}", item.FilePath, failureReason);
            return;
        }

        var outputPath = GenerateOutputPath(item.FilePath);

        // Step 1: FastRepair
        var fastResult = await AttemptFastRepairAsync(item.FilePath, outputPath, timeoutSeconds, progress, ct);
        fastResult.Strategy = "FastRepair";
        item.RepairAttempts.Add(fastResult);

        if (fastResult.Success)
        {
            var validation = await _outputValidator.ValidateOutputAsync(outputPath);
            if (validation.IsValid)
            {
                item.Status = FileStatus.Success;
                _logger.Information("FastRepair succeeded for {FilePath}", item.FilePath);
                return;
            }

            _logger.Warning("FastRepair output invalid for {FilePath}: Duration={HasDuration}, Video={HasVideoStream}",
                item.FilePath, validation.HasDuration, validation.HasVideoStream);
            fastResult.Success = false;
            fastResult.ErrorOutput = (fastResult.ErrorOutput ?? "") + " Output validation failed: " +
                $"HasDuration={validation.HasDuration}, HasVideoStream={validation.HasVideoStream}";
            CleanupFile(outputPath);
        }

        // Check for moov atom error
        var hasMoovError = fastResult.ErrorOutput != null &&
            fastResult.ErrorOutput.Contains("moov atom not found", StringComparison.OrdinalIgnoreCase);

        if (hasMoovError)
        {
            _logger.Information("Moov atom error detected for {FilePath}, skipping FullRepair", item.FilePath);

            if (string.IsNullOrEmpty(config.ReferenceFilePath) || !File.Exists(config.ReferenceFilePath))
            {
                item.Status = FileStatus.Failed;
                item.ErrorMessage = "Plik jest uszkodzony (brak nagłówka wideo). Nie ustawiono pliku referencyjnego – naprawa niemożliwa.";
                _logger.Warning("No reference file configured for moov atom recovery: {FilePath}", item.FilePath);
                return;
            }

            // Skip FullRepair, go directly to UntruncRepair
            var untruncResult = await AttemptUntruncRepairAsync(
                item.FilePath, outputPath, config.ReferenceFilePath, timeoutSeconds, progress, ct);
            item.RepairAttempts.Add(untruncResult);

            if (untruncResult.Success)
            {
                item.Status = FileStatus.Success;
                return;
            }

            item.Status = FileStatus.Failed;
            item.ErrorMessage = BuildFailureMessage(item.RepairAttempts);
            return;
        }

        // Step 2: FullRepair (no moov error path)
        var fullResult = await AttemptFullRepairAsync(item.FilePath, outputPath, timeoutSeconds, progress, ct);
        fullResult.Strategy = "FullRepair";
        item.RepairAttempts.Add(fullResult);

        if (fullResult.Success)
        {
            var validation = await _outputValidator.ValidateOutputAsync(outputPath);
            if (validation.IsValid)
            {
                item.Status = FileStatus.Success;
                _logger.Information("FullRepair succeeded for {FilePath}", item.FilePath);
                return;
            }

            _logger.Warning("FullRepair output invalid for {FilePath}", item.FilePath);
            fullResult.Success = false;
            fullResult.ErrorOutput = (fullResult.ErrorOutput ?? "") + " Output validation failed: " +
                $"HasDuration={validation.HasDuration}, HasVideoStream={validation.HasVideoStream}";
            CleanupFile(outputPath);
        }

        // Step 3: UntruncRepair (fallback after FullRepair failure)
        if (!string.IsNullOrEmpty(config.ReferenceFilePath))
        {
            var untruncResult = await AttemptUntruncRepairAsync(
                item.FilePath, outputPath, config.ReferenceFilePath, timeoutSeconds, progress, ct);
            item.RepairAttempts.Add(untruncResult);

            if (untruncResult.Success)
            {
                item.Status = FileStatus.Success;
                return;
            }
        }

        // All strategies failed
        item.Status = FileStatus.Failed;
        item.ErrorMessage = BuildFailureMessage(item.RepairAttempts);
        _logger.Error("All repair strategies failed for {FilePath}", item.FilePath);
    }

    private async Task<RepairResult> AttemptFastRepairAsync(
        string inputPath, string outputPath, int timeoutSeconds,
        IProgress<double>? progress, CancellationToken ct)
    {
        _logger.Information("Attempting FastRepair for {FilePath}", inputPath);
        var args = $"-y -err_detect ignore_err -i \"{inputPath}\" -c copy \"{outputPath}\"";

        var result = await _ffmpegProcess.RunAsync(args, timeoutSeconds, progress, ct);
        result.OutputPath = outputPath;
        return result;
    }

    private async Task<RepairResult> AttemptFullRepairAsync(
        string inputPath, string outputPath, int timeoutSeconds,
        IProgress<double>? progress, CancellationToken ct)
    {
        _logger.Information("Attempting FullRepair for {FilePath}", inputPath);
        CleanupFile(outputPath);
        var args = $"-y -i \"{inputPath}\" -fflags +genpts -c:v libx264 -c:a aac \"{outputPath}\"";

        var result = await _ffmpegProcess.RunAsync(args, timeoutSeconds, progress, ct);
        result.OutputPath = outputPath;
        return result;
    }

    private async Task<RepairResult> AttemptUntruncRepairAsync(
        string inputPath, string outputPath, string referenceFilePath,
        int timeoutSeconds, IProgress<double>? progress, CancellationToken ct)
    {
        _logger.Information("Attempting UntruncRepair for {FilePath}", inputPath);

        var untruncResult = await _untruncProcess.RunAsync(referenceFilePath, inputPath, timeoutSeconds, ct);
        untruncResult.Strategy = "UntruncRepair";

        if (!untruncResult.Success)
        {
            _logger.Warning("UntruncRepair failed for {FilePath}: {Error}", inputPath, untruncResult.ErrorOutput);
            return untruncResult;
        }

        // Find the untrunc output file (naming varies by untrunc version)
        var untruncOutputPath = FindUntruncOutput(inputPath);

        if (untruncOutputPath == null || !File.Exists(untruncOutputPath))
        {
            _logger.Warning("UntruncRepair output not found for input: {Path}", inputPath);
            untruncResult.Success = false;
            untruncResult.ErrorOutput = $"Nie znaleziono pliku wyjściowego untrunc dla: {inputPath}";
            return untruncResult;
        }

        // Re-process with FFmpeg to ensure valid MP4
        // Use -c:v copy to keep video, -c:a aac to re-encode audio (some dashcam codecs like adpcm_ima_wav aren't supported in MP4)
        _logger.Information("Re-processing untrunc output with FFmpeg for {FilePath}", inputPath);
        CleanupFile(outputPath);
        var reprocessArgs = $"-y -err_detect ignore_err -i \"{untruncOutputPath}\" -c:v copy -c:a aac \"{outputPath}\"";
        var reprocessResult = await _ffmpegProcess.RunAsync(reprocessArgs, timeoutSeconds, progress, ct);

        // Clean up intermediate untrunc output
        CleanupFile(untruncOutputPath);

        if (!reprocessResult.Success)
        {
            _logger.Warning("FFmpeg re-process of untrunc output failed for {FilePath}", inputPath);
            untruncResult.Success = false;
            untruncResult.ErrorOutput = $"FFmpeg re-process failed: {reprocessResult.ErrorOutput}";
            untruncResult.OutputPath = outputPath;
            return untruncResult;
        }

        // Validate the final output
        var validation = await _outputValidator.ValidateOutputAsync(outputPath);
        if (validation.IsValid)
        {
            _logger.Information("UntruncRepair succeeded for {FilePath}", inputPath);
            untruncResult.OutputPath = outputPath;
            return untruncResult;
        }

        _logger.Warning("UntruncRepair output invalid after re-processing for {FilePath}", inputPath);
        untruncResult.Success = false;
        untruncResult.ErrorOutput = (untruncResult.ErrorOutput ?? "") +
            $" Output validation failed after re-processing: HasDuration={validation.HasDuration}, HasVideoStream={validation.HasVideoStream}";
        untruncResult.OutputPath = outputPath;
        CleanupFile(outputPath);
        return untruncResult;
    }

    internal static string GenerateOutputPath(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);

        var candidate = Path.Combine(directory, $"{nameWithoutExt}_fixed.mp4");
        if (!File.Exists(candidate))
            return candidate;

        var suffix = 2;
        while (true)
        {
            candidate = Path.Combine(directory, $"{nameWithoutExt}_fixed_{suffix}.mp4");
            if (!File.Exists(candidate))
                return candidate;
            suffix++;
        }
    }

    private static string? FindUntruncOutput(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var inputFileName = Path.GetFileName(inputPath);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);

        // Untrunc output naming varies by version. Check common patterns:
        var candidates = new[]
        {
            // Pattern 1: <filename>_fixed-dyn.<ext> (appended to full name)
            Path.Combine(directory, $"{inputFileName}_fixed-dyn{extension}"),
            // Pattern 2: <filename>_fixed.<ext>
            Path.Combine(directory, $"{nameWithoutExt}_fixed{extension}"),
            // Pattern 3: <fullname>_fixed-dyn.<ext> (appended to full name including ext)
            Path.Combine(directory, $"{inputFileName}_fixed-dyn"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // Fallback: search for any file matching the pattern *_fixed* in the directory
        try
        {
            var searchPattern = $"{nameWithoutExt}*fixed*";
            var found = Directory.GetFiles(directory, searchPattern)
                .Where(f => !string.Equals(f, inputPath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            return found;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildFailureMessage(List<RepairResult> attempts)
    {
        var messages = attempts
            .Where(a => !a.Success && !string.IsNullOrEmpty(a.ErrorOutput))
            .Select(a => $"{a.Strategy}: {a.ErrorOutput}");
        return "Wszystkie strategie naprawy nie powiodły się. " + string.Join(" | ", messages);
    }

    private void CleanupFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to clean up file: {Path}", path);
        }
    }
}
