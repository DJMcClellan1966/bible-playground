using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Tracks anonymized usage metrics locally to inform app improvements.
/// All data stays on device - no telemetry is sent externally.
/// </summary>
public class UsageMetricsService : IUsageMetricsService
{
    private readonly ILogger<UsageMetricsService> _logger;
    private readonly string _metricsFilePath;
    private readonly object _lock = new();
    private UsageMetrics _metrics;

    public UsageMetricsService(ILogger<UsageMetricsService> logger)
    {
        _logger = logger;
        _metricsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIBibleApp",
            "usage_metrics.json");
        
        _metrics = LoadMetrics();
        _logger.LogInformation("UsageMetricsService initialized. Metrics stored at: {Path}", _metricsFilePath);
    }

    /// <summary>
    /// Track a conversation with a biblical character
    /// </summary>
    public void TrackCharacterConversation(string characterId)
    {
        lock (_lock)
        {
            if (!_metrics.CharacterConversations.ContainsKey(characterId))
                _metrics.CharacterConversations[characterId] = 0;
            
            _metrics.CharacterConversations[characterId]++;
            _metrics.TotalConversations++;
            _metrics.LastActivityDate = DateTime.UtcNow;
            SaveMetrics();
        }
        _logger.LogDebug("Tracked conversation with {Character}", characterId);
    }

    /// <summary>
    /// Track a prayer generation
    /// </summary>
    public void TrackPrayerGenerated(string topic, string? mood = null)
    {
        lock (_lock)
        {
            _metrics.TotalPrayersGenerated++;
            
            // Track topic categories (anonymized)
            var category = CategorizeTopics(topic);
            if (!_metrics.PrayerTopicCategories.ContainsKey(category))
                _metrics.PrayerTopicCategories[category] = 0;
            _metrics.PrayerTopicCategories[category]++;

            if (!string.IsNullOrEmpty(mood))
            {
                if (!_metrics.PrayerMoods.ContainsKey(mood))
                    _metrics.PrayerMoods[mood] = 0;
                _metrics.PrayerMoods[mood]++;
            }

            _metrics.LastActivityDate = DateTime.UtcNow;
            SaveMetrics();
        }
        _logger.LogDebug("Tracked prayer generation: {Category}", CategorizeTopics(topic));
    }

    /// <summary>
    /// Track a Bible verse search or lookup
    /// </summary>
    public void TrackBibleSearch(string book, int? chapter = null)
    {
        lock (_lock)
        {
            _metrics.TotalBibleSearches++;
            
            if (!_metrics.BooksSearched.ContainsKey(book))
                _metrics.BooksSearched[book] = 0;
            _metrics.BooksSearched[book]++;

            _metrics.LastActivityDate = DateTime.UtcNow;
            SaveMetrics();
        }
        _logger.LogDebug("Tracked Bible search: {Book}", book);
    }

    /// <summary>
    /// Track a devotional view
    /// </summary>
    public void TrackDevotionalViewed()
    {
        lock (_lock)
        {
            _metrics.TotalDevotionalsViewed++;
            _metrics.LastActivityDate = DateTime.UtcNow;
            SaveMetrics();
        }
    }

    /// <summary>
    /// Track app session start
    /// </summary>
    public void TrackSessionStart()
    {
        lock (_lock)
        {
            _metrics.TotalSessions++;
            _metrics.SessionStartTime = DateTime.UtcNow;
            _metrics.LastActivityDate = DateTime.UtcNow;
            SaveMetrics();
        }
        _logger.LogDebug("Session started. Total sessions: {Count}", _metrics.TotalSessions);
    }

    /// <summary>
    /// Track app session end
    /// </summary>
    public void TrackSessionEnd()
    {
        lock (_lock)
        {
            if (_metrics.SessionStartTime.HasValue)
            {
                var duration = DateTime.UtcNow - _metrics.SessionStartTime.Value;
                _metrics.TotalSessionMinutes += (int)duration.TotalMinutes;
                _metrics.SessionStartTime = null;
            }
            SaveMetrics();
        }
        _logger.LogDebug("Session ended. Total minutes: {Minutes}", _metrics.TotalSessionMinutes);
    }

    /// <summary>
    /// Track feature usage
    /// </summary>
    public void TrackFeatureUsed(string featureName)
    {
        lock (_lock)
        {
            if (!_metrics.FeatureUsage.ContainsKey(featureName))
                _metrics.FeatureUsage[featureName] = 0;
            _metrics.FeatureUsage[featureName]++;
            SaveMetrics();
        }
    }

    /// <summary>
    /// Get current metrics summary
    /// </summary>
    public UsageMetrics GetMetrics()
    {
        lock (_lock)
        {
            return _metrics.Clone();
        }
    }

    /// <summary>
    /// Get most popular characters
    /// </summary>
    public List<(string CharacterId, int Count)> GetPopularCharacters(int top = 5)
    {
        lock (_lock)
        {
            return _metrics.CharacterConversations
                .OrderByDescending(x => x.Value)
                .Take(top)
                .Select(x => (x.Key, x.Value))
                .ToList();
        }
    }

    /// <summary>
    /// Get usage insights for display
    /// </summary>
    public UsageInsights GetInsights()
    {
        lock (_lock)
        {
            var mostUsedCharacter = _metrics.CharacterConversations
                .OrderByDescending(x => x.Value)
                .FirstOrDefault();

            var mostSearchedBook = _metrics.BooksSearched
                .OrderByDescending(x => x.Value)
                .FirstOrDefault();

            var avgSessionMinutes = _metrics.TotalSessions > 0
                ? _metrics.TotalSessionMinutes / _metrics.TotalSessions
                : 0;

            return new UsageInsights
            {
                TotalConversations = _metrics.TotalConversations,
                TotalPrayers = _metrics.TotalPrayersGenerated,
                TotalBibleSearches = _metrics.TotalBibleSearches,
                TotalDevotionalsViewed = _metrics.TotalDevotionalsViewed,
                TotalSessions = _metrics.TotalSessions,
                AverageSessionMinutes = avgSessionMinutes,
                FavoriteCharacter = mostUsedCharacter.Key,
                FavoriteCharacterCount = mostUsedCharacter.Value,
                MostSearchedBook = mostSearchedBook.Key,
                MostSearchedBookCount = mostSearchedBook.Value,
                DaysSinceFirstUse = _metrics.FirstUseDate.HasValue
                    ? (int)(DateTime.UtcNow - _metrics.FirstUseDate.Value).TotalDays
                    : 0
            };
        }
    }

    /// <summary>
    /// Reset all metrics (user request)
    /// </summary>
    public void ResetMetrics()
    {
        lock (_lock)
        {
            _metrics = new UsageMetrics
            {
                FirstUseDate = DateTime.UtcNow
            };
            SaveMetrics();
        }
        _logger.LogInformation("Usage metrics reset by user");
    }

    /// <summary>
    /// Export metrics as JSON (for user transparency)
    /// </summary>
    public string ExportMetrics()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_metrics, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }

    private string CategorizeTopics(string topic)
    {
        var lower = topic.ToLowerInvariant();
        
        if (lower.Contains("gratitude") || lower.Contains("thankful") || lower.Contains("blessing"))
            return "Gratitude";
        if (lower.Contains("healing") || lower.Contains("health") || lower.Contains("sick"))
            return "Healing";
        if (lower.Contains("guidance") || lower.Contains("direction") || lower.Contains("wisdom"))
            return "Guidance";
        if (lower.Contains("peace") || lower.Contains("anxiety") || lower.Contains("worry"))
            return "Peace";
        if (lower.Contains("family") || lower.Contains("marriage") || lower.Contains("children"))
            return "Family";
        if (lower.Contains("work") || lower.Contains("job") || lower.Contains("career"))
            return "Work/Career";
        if (lower.Contains("forgiveness") || lower.Contains("repent"))
            return "Forgiveness";
        if (lower.Contains("strength") || lower.Contains("courage"))
            return "Strength";
        if (lower.Contains("love") || lower.Contains("relationship"))
            return "Love/Relationships";
        if (lower.Contains("protection") || lower.Contains("safety"))
            return "Protection";
        
        return "General";
    }

    private UsageMetrics LoadMetrics()
    {
        try
        {
            if (File.Exists(_metricsFilePath))
            {
                var json = File.ReadAllText(_metricsFilePath);
                var metrics = JsonSerializer.Deserialize<UsageMetrics>(json);
                return metrics ?? CreateNewMetrics();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load metrics, starting fresh");
        }
        
        return CreateNewMetrics();
    }

    private UsageMetrics CreateNewMetrics()
    {
        return new UsageMetrics
        {
            FirstUseDate = DateTime.UtcNow
        };
    }

    private void SaveMetrics()
    {
        try
        {
            var directory = Path.GetDirectoryName(_metricsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_metrics, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_metricsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save metrics");
        }
    }
}

/// <summary>
/// Usage metrics data structure (stored locally, anonymized)
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

/// <summary>
/// Summarized insights from usage data
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
/// Interface for usage metrics service
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
