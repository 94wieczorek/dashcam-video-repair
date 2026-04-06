using System.IO;
using Serilog;

namespace DashcamVideoRepair.Infrastructure;

/// <summary>
/// Factory for creating a configured Serilog logger with file sink.
/// </summary>
public static class LoggingConfig
{
    /// <summary>
    /// Creates a Serilog ILogger configured to write to the logs/ subdirectory
    /// in the application directory with daily rolling files.
    /// </summary>
    public static ILogger CreateLogger()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var logPath = Path.Combine(appDirectory, "logs", "dashcam-repair-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
