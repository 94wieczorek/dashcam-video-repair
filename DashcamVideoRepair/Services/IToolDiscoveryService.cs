namespace DashcamVideoRepair.Services;

/// <summary>
/// Discovers and validates external tool paths (ffmpeg, ffprobe).
/// </summary>
public interface IToolDiscoveryService
{
    /// <summary>
    /// Discovers the ffmpeg.exe path by checking ConfigStore, application directory, and system PATH.
    /// Returns the discovered path, or null if not found.
    /// </summary>
    Task<string?> DiscoverFfmpegAsync();

    /// <summary>
    /// Validates that the given path points to an existing ffmpeg.exe and that ffprobe.exe is co-located.
    /// </summary>
    bool ValidateFfmpegPath(string path);
}
