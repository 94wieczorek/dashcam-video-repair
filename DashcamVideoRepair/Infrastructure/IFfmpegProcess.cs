using DashcamVideoRepair.Models;

namespace DashcamVideoRepair.Infrastructure;

public interface IFfmpegProcess
{
    Task<RepairResult> RunAsync(string args, int timeoutSeconds, IProgress<double>? progress, CancellationToken cancellationToken);
    Task<string> ProbeAsync(string filePath, int timeoutSeconds, CancellationToken cancellationToken);
}
