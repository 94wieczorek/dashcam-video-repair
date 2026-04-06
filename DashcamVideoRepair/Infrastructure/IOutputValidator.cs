namespace DashcamVideoRepair.Infrastructure;

public interface IOutputValidator
{
    Task<(bool IsValid, bool HasDuration, bool HasVideoStream)> ValidateOutputAsync(string filePath);
}
