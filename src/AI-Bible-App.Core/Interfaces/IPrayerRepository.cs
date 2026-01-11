using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Repository for managing prayers
/// </summary>
public interface IPrayerRepository
{
    Task<Prayer> GetPrayerAsync(string prayerId);
    Task<List<Prayer>> GetAllPrayersAsync();
    Task<List<SavedPrayer>> GetAllForUserAsync(string userId);
    Task<List<Prayer>> GetPrayersByTopicAsync(string topic);
    Task SavePrayerAsync(Prayer prayer);
    Task SaveAsync(SavedPrayer prayer);
    Task DeletePrayerAsync(string prayerId);
}
