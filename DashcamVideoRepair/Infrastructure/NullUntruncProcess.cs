using DashcamVideoRepair.Models;

namespace DashcamVideoRepair.Infrastructure;

/// <summary>
/// A no-op implementation of IUntruncProcess used when untrunc is not configured.
/// Always returns a failure result indicating untrunc is not available.
/// </summary>
public class NullUntruncProcess : IUntruncProcess
{
    public Task<RepairResult> RunAsync(string referenceFile, string inputFile, int timeoutSeconds, CancellationToken cancellationToken)
    {
        return Task.FromResult(new RepairResult
        {
            Strategy = "UntruncRepair",
            Success = false,
            ErrorOutput = "Untrunc nie jest skonfigurowany. Ustaw ścieżkę do pliku wykonywalnego untrunc w Ustawieniach."
        });
    }
}
