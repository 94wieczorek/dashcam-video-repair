using DashcamVideoRepair.Models;

namespace DashcamVideoRepair.Infrastructure;

public interface IUntruncProcess
{
    Task<RepairResult> RunAsync(string referenceFile, string inputFile, int timeoutSeconds, CancellationToken cancellationToken);
}
