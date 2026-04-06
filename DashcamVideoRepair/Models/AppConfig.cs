namespace DashcamVideoRepair.Models;

public class AppConfig
{
    public string? FfmpegPath { get; set; }
    public string? UntruncPath { get; set; }
    public string? ReferenceFilePath { get; set; }
    public string? LastUsedFolder { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
}
