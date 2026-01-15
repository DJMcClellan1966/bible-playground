namespace AI_Bible_App.Core.Models;

/// <summary>
/// Usage metrics data structure (stored locally, anonymized).
/// </summary>
public class UsageMetrics
{
    public DateTime? FirstUseDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public DateTime? SessionStartTime { get; set; }

    public int TotalSessions { get; set; }
    public int TotalSessionMinutes { get; set; }
    public int TotalConversations { get; set; }
    public int TotalPrayersGenerated { get; set; }
    public int TotalBibleSearches { get; set; }
    public int TotalDevotionalsViewed { get; set; }

    // Character usage (character ID -> count)
    public Dictionary<string, int> CharacterConversations { get; set; } = new();

    // Prayer topic categories (anonymized)
    public Dictionary<string, int> PrayerTopicCategories { get; set; } = new();
    public Dictionary<string, int> PrayerMoods { get; set; } = new();

    // Bible book searches
    public Dictionary<string, int> BooksSearched { get; set; } = new();

    // Feature usage tracking
    public Dictionary<string, int> FeatureUsage { get; set; } = new();

    public UsageMetrics Clone()
    {
        return new UsageMetrics
        {
            FirstUseDate = FirstUseDate,
            LastActivityDate = LastActivityDate,
            TotalSessions = TotalSessions,
            TotalSessionMinutes = TotalSessionMinutes,
            TotalConversations = TotalConversations,
            TotalPrayersGenerated = TotalPrayersGenerated,
            TotalBibleSearches = TotalBibleSearches,
            TotalDevotionalsViewed = TotalDevotionalsViewed,
            CharacterConversations = new Dictionary<string, int>(CharacterConversations),
            PrayerTopicCategories = new Dictionary<string, int>(PrayerTopicCategories),
            PrayerMoods = new Dictionary<string, int>(PrayerMoods),
            BooksSearched = new Dictionary<string, int>(BooksSearched),
            FeatureUsage = new Dictionary<string, int>(FeatureUsage)
        };
    }
}
