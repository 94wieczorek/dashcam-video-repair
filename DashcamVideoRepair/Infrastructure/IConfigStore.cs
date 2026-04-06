using DashcamVideoRepair.Models;

namespace DashcamVideoRepair.Infrastructure;

public interface IConfigStore
{
    Task<AppConfig> LoadAsync();
    Task SaveAsync(AppConfig config);
}
