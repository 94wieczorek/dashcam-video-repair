using DashcamVideoRepair.Infrastructure;
using DashcamVideoRepair.Models;
using DashcamVideoRepair.Services;
using DashcamVideoRepair.ViewModels;
using FluentAssertions;
using Moq;
using Serilog;

namespace DashcamVideoRepair.Tests.Unit;

public class MainViewModelTests
{
    private readonly Mock<IFfmpegProcess> _ffmpegProcess = new();
    private readonly Mock<IUntruncProcess> _untruncProcess = new();
    private readonly Mock<IOutputValidator> _outputValidator = new();
    private readonly Mock<IFileValidator> _fileValidator = new();
    private readonly Mock<IConfigStore> _configStore = new();
    private readonly Mock<ILogger> _logger = new();

    private MainViewModel CreateViewModel()
    {
        var repairService = new VideoRepairService(
            _ffmpegProcess.Object,
            _untruncProcess.Object,
            _outputValidator.Object,
            _fileValidator.Object,
            _configStore.Object,
            _logger.Object);

        return new MainViewModel(repairService, _configStore.Object, _logger.Object);
    }

    [Fact]
    public void AddFiles_WithMovAndMp4_AddsToQueue()
    {
        var vm = CreateViewModel();
        var files = new[] { @"C:\videos\clip1.mov", @"C:\videos\clip2.mp4" };

        vm.AddFiles(files);

        vm.RepairQueue.Should().HaveCount(2);
        vm.RepairQueue.Should().AllSatisfy(item => item.Status.Should().Be(FileStatus.Pending));
    }

    [Fact]
    public void AddFiles_WithUnsupportedExtensions_RejectsAndWarns()
    {
        var vm = CreateViewModel();
        var files = new[] { @"C:\videos\clip.avi", @"C:\videos\clip.mkv", @"C:\videos\good.mp4" };

        vm.AddFiles(files);

        vm.RepairQueue.Should().HaveCount(1);
        vm.RepairQueue[0].FileName.Should().Be("good.mp4");
        vm.WarningMessage.Should().Contain("clip.avi");
        vm.WarningMessage.Should().Contain("clip.mkv");
    }

    [Fact]
    public void AddFiles_WithDuplicates_SkipsAndWarns()
    {
        var vm = CreateViewModel();
        vm.AddFiles(new[] { @"C:\videos\clip.mp4" });

        vm.AddFiles(new[] { @"C:\videos\clip.mp4" });

        vm.RepairQueue.Should().HaveCount(1);
        vm.WarningMessage.Should().Contain("duplikaty");
    }

    [Fact]
    public void AddFiles_CaseInsensitiveExtension_AcceptsMOV()
    {
        var vm = CreateViewModel();
        vm.AddFiles(new[] { @"C:\videos\clip.MOV", @"C:\videos\clip2.Mp4" });

        vm.RepairQueue.Should().HaveCount(2);
    }

    [Fact]
    public void AddFiles_EmptyList_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.AddFiles(Array.Empty<string>());

        vm.RepairQueue.Should().BeEmpty();
        vm.WarningMessage.Should().BeNull();
    }

    [Fact]
    public void AddFiles_MixedValid_And_Invalid_FiltersCorrectly()
    {
        var vm = CreateViewModel();
        var files = new[]
        {
            @"C:\videos\a.mp4",
            @"C:\videos\b.txt",
            @"C:\videos\c.mov",
            @"C:\videos\d.jpg"
        };

        vm.AddFiles(files);

        vm.RepairQueue.Should().HaveCount(2);
        vm.RepairQueue.Select(i => i.FileName).Should().BeEquivalentTo("a.mp4", "c.mov");
        vm.WarningMessage.Should().Contain("b.txt");
        vm.WarningMessage.Should().Contain("d.jpg");
    }

    [Fact]
    public void IsProcessing_DefaultsFalse()
    {
        var vm = CreateViewModel();
        vm.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public void BatchSummary_DefaultsNull()
    {
        var vm = CreateViewModel();
        vm.BatchSummary.Should().BeNull();
    }

    [Fact]
    public void AddFiles_SetsFileNameFromPath()
    {
        var vm = CreateViewModel();
        vm.AddFiles(new[] { @"C:\some\deep\path\dashcam_2024.mp4" });

        vm.RepairQueue[0].FileName.Should().Be("dashcam_2024.mp4");
    }

    [Fact]
    public void AddFolder_NonExistentFolder_SetsWarning()
    {
        var vm = CreateViewModel();
        vm.AddFolder(@"C:\nonexistent\folder\path_" + Guid.NewGuid());

        vm.RepairQueue.Should().BeEmpty();
        vm.WarningMessage.Should().Contain("Nie znaleziono folderu");
    }
}
