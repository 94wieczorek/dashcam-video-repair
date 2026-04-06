using System.IO;
using DashcamVideoRepair.Services;
using FluentAssertions;

namespace DashcamVideoRepair.Tests.Unit;

public class OutputFileNamingTests : IDisposable
{
    private readonly string _tempDir;

    public OutputFileNamingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dashcam_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void GenerateOutputPath_NoConflict_ReturnsFixedMp4()
    {
        var input = Path.Combine(_tempDir, "video.mov");

        var result = VideoRepairService.GenerateOutputPath(input);

        result.Should().Be(Path.Combine(_tempDir, "video_fixed.mp4"));
    }

    [Fact]
    public void GenerateOutputPath_Mp4Input_ReturnsFixedMp4()
    {
        var input = Path.Combine(_tempDir, "clip.mp4");

        var result = VideoRepairService.GenerateOutputPath(input);

        result.Should().Be(Path.Combine(_tempDir, "clip_fixed.mp4"));
    }

    [Fact]
    public void GenerateOutputPath_OutputExists_ReturnsSuffix2()
    {
        var input = Path.Combine(_tempDir, "video.mov");
        File.WriteAllText(Path.Combine(_tempDir, "video_fixed.mp4"), "");

        var result = VideoRepairService.GenerateOutputPath(input);

        result.Should().Be(Path.Combine(_tempDir, "video_fixed_2.mp4"));
    }

    [Fact]
    public void GenerateOutputPath_MultipleConflicts_IncrementsUntilUnique()
    {
        var input = Path.Combine(_tempDir, "video.mov");
        File.WriteAllText(Path.Combine(_tempDir, "video_fixed.mp4"), "");
        File.WriteAllText(Path.Combine(_tempDir, "video_fixed_2.mp4"), "");
        File.WriteAllText(Path.Combine(_tempDir, "video_fixed_3.mp4"), "");

        var result = VideoRepairService.GenerateOutputPath(input);

        result.Should().Be(Path.Combine(_tempDir, "video_fixed_4.mp4"));
    }

    [Fact]
    public void GenerateOutputPath_OutputInSameDirectoryAsInput()
    {
        var input = Path.Combine(_tempDir, "video.mov");

        var result = VideoRepairService.GenerateOutputPath(input);

        Path.GetDirectoryName(result).Should().Be(_tempDir);
    }

    [Fact]
    public void GenerateOutputPath_AlwaysProducesMp4Extension()
    {
        var movInput = Path.Combine(_tempDir, "clip.mov");
        var mp4Input = Path.Combine(_tempDir, "clip2.mp4");

        var result1 = VideoRepairService.GenerateOutputPath(movInput);
        var result2 = VideoRepairService.GenerateOutputPath(mp4Input);

        Path.GetExtension(result1).Should().Be(".mp4");
        Path.GetExtension(result2).Should().Be(".mp4");
    }
}
