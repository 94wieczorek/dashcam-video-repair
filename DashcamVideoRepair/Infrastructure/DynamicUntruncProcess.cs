using DashcamVideoRepair.Models;
using Serilog;

namespace DashcamVideoRepair.Infrastructure;

/// <summary>
/// Dynamically resolves the untrunc executable path from config on each call,
/// so that settings changes take effect without restarting the app.
/// </summary>
public class DynamicUntruncProcess : IUntruncProcess
{
    private readonly IConfigStore _configStore;
    private readonly ILogger _logger;

    public DynamicUntruncProcess(IConfigStore configStore, ILogger logger)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RepairResult> RunAsync(string referenceFile, string inputFile, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var config = await _configStore.LoadAsync();
        var untruncPath = config.UntruncPath;

        if (string.IsNullOrEmpty(untruncPath) || !System.IO.File.Exists(untruncPath))
        {
            return new RepairResult
            {
                Strategy = "UntruncRepair",
                Success = false,
                ErrorOutput = "Untrunc nie jest skonfigurowany. Ustaw ścieżkę do pliku wykonywalnego untrunc w Ustawieniach."
            };
        }

        var realProcess = new UntruncProcess(untruncPath, _logger);
        return await realProcess.RunAsync(referenceFile, inputFile, timeoutSeconds, cancellationToken);
    }
}
