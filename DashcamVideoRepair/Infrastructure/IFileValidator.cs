namespace DashcamVideoRepair.Infrastructure;

public interface IFileValidator
{
    Task<(bool IsValid, string? FailureReason)> ValidateAsync(string filePath);
}
