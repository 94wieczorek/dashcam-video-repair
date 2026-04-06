using DashcamVideoRepair.Infrastructure;
using FluentAssertions;
using Moq;
using Serilog;

namespace DashcamVideoRepair.Tests.Unit;

public class OutputValidatorTests
{
    private readonly Mock<IFfmpegProcess> _mockFfmpeg;
    private readonly ILogger _logger;
    private readonly OutputValidator _validator;

    public OutputValidatorTests()
    {
        _mockFfmpeg = new Mock<IFfmpegProcess>();
        _logger = new LoggerConfiguration().CreateLogger();
        _validator = new OutputValidator(_mockFfmpeg.Object, _logger);
    }

    [Fact]
    public async Task ValidateOutputAsync_ValidVideoWithDuration_ReturnsAllTrue()
    {
        var json = """
        {
            "format": { "duration": "120.500000" },
            "streams": [
                { "codec_type": "video" },
                { "codec_type": "audio" }
            ]
        }
        """;
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeTrue();
        result.HasDuration.Should().BeTrue();
        result.HasVideoStream.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOutputAsync_ZeroDuration_ReturnsInvalid()
    {
        var json = """
        {
            "format": { "duration": "0.000000" },
            "streams": [{ "codec_type": "video" }]
        }
        """;
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeFalse();
        result.HasDuration.Should().BeFalse();
        result.HasVideoStream.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOutputAsync_NoVideoStream_ReturnsInvalid()
    {
        var json = """
        {
            "format": { "duration": "60.000000" },
            "streams": [{ "codec_type": "audio" }]
        }
        """;
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeFalse();
        result.HasDuration.Should().BeTrue();
        result.HasVideoStream.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOutputAsync_EmptyOutput_ReturnsAllFalse()
    {
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeFalse();
        result.HasDuration.Should().BeFalse();
        result.HasVideoStream.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOutputAsync_NoStreams_ReturnsInvalid()
    {
        var json = """
        {
            "format": { "duration": "30.000000" }
        }
        """;
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeFalse();
        result.HasDuration.Should().BeTrue();
        result.HasVideoStream.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOutputAsync_ProbeThrows_ReturnsAllFalse()
    {
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ffprobe not found"));

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeFalse();
        result.HasDuration.Should().BeFalse();
        result.HasVideoStream.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOutputAsync_InvalidJson_ReturnsAllFalse()
    {
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json at all");

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeFalse();
        result.HasDuration.Should().BeFalse();
        result.HasVideoStream.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOutputAsync_NoDurationField_ReturnsInvalid()
    {
        var json = """
        {
            "format": {},
            "streams": [{ "codec_type": "video" }]
        }
        """;
        _mockFfmpeg.Setup(f => f.ProbeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var result = await _validator.ValidateOutputAsync("test.mp4");

        result.IsValid.Should().BeFalse();
        result.HasDuration.Should().BeFalse();
        result.HasVideoStream.Should().BeTrue();
    }
}
