namespace DashcamVideoRepair.Models;

public class RepairResult
{
    public string Strategy { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorOutput { get; set; }
    public string? OutputPath { get; set; }
}
