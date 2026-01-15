using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Summarized insights from usage data.
/// </summary>
public class UsageInsights
{
    public int TotalConversations { get; set; }
    public int TotalPrayers { get; set; }
    public int TotalBibleSearches { get; set; }
    public int TotalDevotionalsViewed { get; set; }
    public int TotalSessions { get; set; }
    public int AverageSessionMinutes { get; set; }
    public string? FavoriteCharacter { get; set; }
    public int FavoriteCharacterCount { get; set; }
    public string? MostSearchedBook { get; set; }
    public int MostSearchedBookCount { get; set; }
    public int DaysSinceFirstUse { get; set; }
}

/// <summary>
/// Interface for usage metrics service.
/// </summary>
public interface IUsageMetricsService
{
    void TrackCharacterConversation(string characterId);
    void TrackPrayerGenerated(string topic, string? mood = null);
    void TrackBibleSearch(string book, int? chapter = null);
    void TrackDevotionalViewed();
    void TrackSessionStart();
    void TrackSessionEnd();
    void TrackFeatureUsed(string featureName);
    UsageMetrics GetMetrics();
    List<(string CharacterId, int Count)> GetPopularCharacters(int top = 5);
    UsageInsights GetInsights();
    void ResetMetrics();
    string ExportMetrics();
}
