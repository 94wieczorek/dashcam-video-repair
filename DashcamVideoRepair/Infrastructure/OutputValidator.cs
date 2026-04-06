using System.Text.Json;
using Serilog;

namespace DashcamVideoRepair.Infrastructure;

public class OutputValidator : IOutputValidator
{
    private readonly IFfmpegProcess _ffmpegProcess;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OutputValidator(IFfmpegProcess ffmpegProcess, ILogger logger)
    {
        _ffmpegProcess = ffmpegProcess ?? throw new ArgumentNullException(nameof(ffmpegProcess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(bool IsValid, bool HasDuration, bool HasVideoStream)> ValidateOutputAsync(string filePath)
    {
        try
        {
            var json = await _ffmpegProcess.ProbeAsync(filePath, 30, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.Warning("ffprobe returned empty output for {FilePath}", filePath);
                return (false, false, false);
            }

            var probeResult = JsonSerializer.Deserialize<FfprobeResult>(json, JsonOptions);
            if (probeResult is null)
            {
                _logger.Warning("Failed to deserialize ffprobe output for {FilePath}", filePath);
                return (false, false, false);
            }

            var hasDuration = false;
            if (probeResult.Format?.Duration is string durationStr
                && double.TryParse(durationStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var duration))
            {
                hasDuration = duration > 0;
            }

            var hasVideoStream = probeResult.Streams?.Any(s =>
                string.Equals(s.CodecType, "video", StringComparison.OrdinalIgnoreCase)) ?? false;

            var isValid = hasDuration && hasVideoStream;

            _logger.Information("Output validation for {FilePath}: IsValid={IsValid}, HasDuration={HasDuration}, HasVideoStream={HasVideoStream}",
                filePath, isValid, hasDuration, hasVideoStream);

            return (isValid, hasDuration, hasVideoStream);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Output validation failed for {FilePath}", filePath);
            return (false, false, false);
        }
    }

    private sealed class FfprobeResult
    {
        public FfprobeFormat? Format { get; set; }
        public List<FfprobeStream>? Streams { get; set; }
    }

    private sealed class FfprobeFormat
    {
        public string? Duration { get; set; }
    }

    private sealed class FfprobeStream
    {
        [System.Text.Json.Serialization.JsonPropertyName("codec_type")]
        public string? CodecType { get; set; }
    }
}
