using System.Diagnostics;
using System.Text;
using DashcamVideoRepair.Models;
using Serilog;

namespace DashcamVideoRepair.Infrastructure;

public class UntruncProcess : IUntruncProcess
{
    private readonly string _untruncPath;
    private readonly ILogger _logger;

    public UntruncProcess(string untruncPath, ILogger logger)
    {
        _untruncPath = untruncPath ?? throw new ArgumentNullException(nameof(untruncPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RepairResult> RunAsync(string referenceFile, string inputFile, int timeoutSeconds, CancellationToken cancellationToken)
    {
        _logger.Information("Running untrunc: {UntruncPath} \"{ReferenceFile}\" \"{InputFile}\"", _untruncPath, referenceFile, inputFile);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var psi = new ProcessStartInfo
        {
            FileName = _untruncPath,
            Arguments = $"\"{referenceFile}\" \"{inputFile}\"",
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
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            KillProcess(process);
            var timeoutError = $"Untrunc process timed out after {timeoutSeconds} seconds";
            _logger.Error(timeoutError);
            return new RepairResult
            {
                Strategy = "UntruncRepair",
                Success = false,
                ErrorOutput = timeoutError
            };
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        var combinedOutput = CombineOutput(stdout, stderr);
        var exitCode = process.ExitCode;

        if (exitCode != 0)
        {
            _logger.Warning("Untrunc exited with code {ExitCode}. Output: {Output}", exitCode, combinedOutput);
            return new RepairResult
            {
                Strategy = "UntruncRepair",
                Success = false,
                ErrorOutput = combinedOutput
            };
        }

        _logger.Information("Untrunc completed successfully for {InputFile}", inputFile);
        return new RepairResult
        {
            Strategy = "UntruncRepair",
            Success = true,
            ErrorOutput = string.IsNullOrWhiteSpace(combinedOutput) ? null : combinedOutput
        };
    }

    private static string CombineOutput(string stdout, string stderr)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(stdout))
            sb.AppendLine(stdout.Trim());

        if (!string.IsNullOrWhiteSpace(stderr))
            sb.AppendLine(stderr.Trim());

        return sb.ToString().Trim();
    }

    private void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.Warning("Killed untrunc process (PID: {ProcessId})", process.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to kill untrunc process");
        }
    }
}
