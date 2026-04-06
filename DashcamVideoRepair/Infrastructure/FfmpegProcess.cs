using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DashcamVideoRepair.Models;
using Serilog;

namespace DashcamVideoRepair.Infrastructure;

public partial class FfmpegProcess : IFfmpegProcess
{
    private readonly string _ffmpegPath;
    private readonly ILogger _logger;

    private static readonly Regex TimeRegex = CreateTimeRegex();

    [GeneratedRegex(@"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2,3})", RegexOptions.Compiled)]
    private static partial Regex CreateTimeRegex();

    public FfmpegProcess(string ffmpegPath, ILogger logger)
    {
        _ffmpegPath = ffmpegPath ?? throw new ArgumentNullException(nameof(ffmpegPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RepairResult> RunAsync(string args, int timeoutSeconds, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(args, timeoutSeconds, progress, cancellationToken);

        if (!result.Success
            && result.ErrorOutput != null
            && result.ErrorOutput.Contains("Invalid data found when processing input", StringComparison.OrdinalIgnoreCase)
            && !ContainsMoovAtomError(result.ErrorOutput))
        {
            _logger.Warning("FFmpeg reported 'Invalid data found when processing input', retrying with extended flags");

            var retryArgs = PrependExtendedFlags(args);
            result = await ExecuteAsync(retryArgs, timeoutSeconds, progress, cancellationToken);
        }

        return result;
    }

    public async Task<string> ProbeAsync(string filePath, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var ffprobePath = GetFfprobePath();

        var arguments = $"-v error -show_entries format=duration -show_entries stream=codec_type -of json \"{filePath}\"";

        _logger.Information("Running ffprobe: {FfprobePath} {Arguments}", ffprobePath, arguments);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var psi = new ProcessStartInfo
        {
            FileName = ffprobePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.Warning("ffprobe stderr: {Stderr}", stderr);
        }

        return stdout;
    }

    private async Task<RepairResult> ExecuteAsync(string args, int timeoutSeconds, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        _logger.Information("Running FFmpeg: {FfmpegPath} {Arguments}", _ffmpegPath, args);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = true
        };

        var stderrBuilder = new StringBuilder();
        var totalDuration = EstimateDurationFromArgs(args);

        using var process = new Process { StartInfo = psi };
        process.Start();

        try
        {
            await ReadStderrAsync(process, stderrBuilder, totalDuration, progress, linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            KillProcess(process);
            var timeoutError = $"FFmpeg process timed out after {timeoutSeconds} seconds";
            _logger.Error(timeoutError);
            return new RepairResult
            {
                Strategy = string.Empty,
                Success = false,
                ErrorOutput = timeoutError
            };
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            throw;
        }

        var stderr = stderrBuilder.ToString();
        var exitCode = process.ExitCode;

        if (exitCode != 0)
        {
            _logger.Warning("FFmpeg exited with code {ExitCode}. Stderr: {Stderr}", exitCode, stderr);
            return new RepairResult
            {
                Strategy = string.Empty,
                Success = false,
                ErrorOutput = stderr
            };
        }

        // Even on exit code 0, check for moov atom error in stderr and include it
        var result = new RepairResult
        {
            Strategy = string.Empty,
            Success = true,
            ErrorOutput = ContainsMoovAtomError(stderr) ? stderr : null
        };

        return result;
    }

    private async Task ReadStderrAsync(Process process, StringBuilder stderrBuilder, TimeSpan? totalDuration, IProgress<double>? progress, CancellationToken ct)
    {
        var reader = process.StandardError;
        var buffer = new char[4096];

        while (!ct.IsCancellationRequested)
        {
            var bytesRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (bytesRead == 0)
                break;

            var chunk = new string(buffer, 0, bytesRead);
            stderrBuilder.Append(chunk);

            if (progress != null && totalDuration.HasValue && totalDuration.Value.TotalSeconds > 0)
            {
                var currentTime = ParseLastTime(chunk);
                if (currentTime.HasValue)
                {
                    var percentage = Math.Min(100.0, (currentTime.Value.TotalSeconds / totalDuration.Value.TotalSeconds) * 100.0);
                    progress.Report(percentage);
                }
            }
        }
    }

    internal static TimeSpan? ParseLastTime(string text)
    {
        TimeSpan? lastTime = null;
        var matches = TimeRegex.Matches(text);

        foreach (Match match in matches)
        {
            if (match.Success
                && int.TryParse(match.Groups[1].Value, out var hours)
                && int.TryParse(match.Groups[2].Value, out var minutes)
                && int.TryParse(match.Groups[3].Value, out var seconds)
                && int.TryParse(match.Groups[4].Value, out var fraction))
            {
                // Normalize fraction to milliseconds
                var fractionStr = match.Groups[4].Value;
                var milliseconds = fractionStr.Length == 2 ? fraction * 10 : fraction;

                lastTime = new TimeSpan(0, hours, minutes, seconds, milliseconds);
            }
        }

        return lastTime;
    }

    internal static bool ContainsMoovAtomError(string stderr)
    {
        return stderr.Contains("moov atom not found", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsInvalidDataError(string stderr)
    {
        return stderr.Contains("Invalid data found when processing input", StringComparison.OrdinalIgnoreCase);
    }

    internal static string PrependExtendedFlags(string originalArgs)
    {
        // Insert extended flags before the -i argument
        const string extendedFlags = "-fflags +igndts -analyzeduration 100M -probesize 100M";
        var iIndex = originalArgs.IndexOf("-i ", StringComparison.Ordinal);

        if (iIndex >= 0)
        {
            return string.Concat(originalArgs.AsSpan(0, iIndex), extendedFlags, " ", originalArgs.AsSpan(iIndex));
        }

        // If no -i found, just prepend
        return $"{extendedFlags} {originalArgs}";
    }

    private static TimeSpan? EstimateDurationFromArgs(string args)
    {
        // FFmpeg doesn't provide total duration in stderr easily.
        // The caller (VideoRepairService) should provide duration via IProgress context.
        // For now, return null — progress will not be reported without a known duration.
        return null;
    }

    private string GetFfprobePath()
    {
        var directory = Path.GetDirectoryName(_ffmpegPath);
        if (directory != null)
        {
            var ffprobeName = Path.GetFileName(_ffmpegPath).Replace("ffmpeg", "ffprobe", StringComparison.OrdinalIgnoreCase);
            return Path.Combine(directory, ffprobeName);
        }

        return "ffprobe";
    }

    private void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.Warning("Killed FFmpeg process (PID: {ProcessId})", process.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to kill FFmpeg process");
        }
    }
}
