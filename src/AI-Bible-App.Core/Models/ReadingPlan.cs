namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents a Bible reading plan with metadata and daily readings
/// </summary>
public class ReadingPlan
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public ReadingPlanType Type { get; set; } = ReadingPlanType.Canonical;
    public ReadingPlanDifficulty Difficulty { get; set; } = ReadingPlanDifficulty.Medium;
    
    /// <summary>
    /// Estimated reading time per day in minutes
    /// </summary>
    public int EstimatedMinutesPerDay { get; set; } = 15;
    
    /// <summary>
    /// The daily readings for this plan
    /// </summary>
    public List<ReadingPlanDay> Days { get; set; } = new();
    
    /// <summary>
    /// Tags for filtering (e.g., "Old Testament", "Gospels", "Wisdom")
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// A single day's reading in a plan
/// </summary>
public class ReadingPlanDay
{
    public int DayNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Scripture passages for this day (e.g., "Genesis 1-3", "Psalm 1")
    /// </summary>
    public List<string> Passages { get; set; } = new();
    
    /// <summary>
    /// Optional reflection question or theme
    /// </summary>
    public string? ReflectionPrompt { get; set; }
    
    /// <summary>
    /// Optional key verse reference
    /// </summary>
    public string? KeyVerse { get; set; }
    
    /// <summary>
    /// Estimated reading time in minutes
    /// </summary>
    public int EstimatedMinutes { get; set; } = 15;
}

/// <summary>
/// User's progress through a reading plan
/// </summary>
public class UserReadingProgress
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "default";
    public string PlanId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Set of completed day numbers
    /// </summary>
    public HashSet<int> CompletedDays { get; set; } = new();
    
    /// <summary>
    /// Current day the user is on (1-based)
    /// </summary>
    public int CurrentDay { get; set; } = 1;
    
    /// <summary>
    /// Notes for each day (day number -> note)
    /// </summary>
    public Dictionary<int, string> DayNotes { get; set; } = new();
    
    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Calculate completion percentage
    /// </summary>
    public double CompletionPercentage => CompletedDays.Count > 0 && TotalDays > 0
        ? Math.Round((double)CompletedDays.Count / TotalDays * 100, 1)
        : 0;
    
    /// <summary>
    /// Total days in the plan (set from ReadingPlan)
    /// </summary>
    public int TotalDays { get; set; }
    
    /// <summary>
    /// Days remaining
    /// </summary>
    public int DaysRemaining => Math.Max(0, TotalDays - CompletedDays.Count);
    
    /// <summary>
    /// Current streak of consecutive days
    /// </summary>
    public int CurrentStreak { get; set; }
    
    /// <summary>
    /// Longest streak achieved
    /// </summary>
    public int LongestStreak { get; set; }
}

/// <summary>
/// Types of reading plans
/// </summary>
public enum ReadingPlanType
{
    Canonical,      // Genesis to Revelation
    Chronological,  // Events in historical order
    Thematic,       // Topic-based (e.g., Prayer, Faith)
    Gospel,         // Focus on Jesus' life
    Wisdom,         // Proverbs, Psalms, Ecclesiastes
    Prophets,       // Major and minor prophets
    NewTestament,   // NT only
    OldTestament,   // OT only
    Custom          // User-created
}

/// <summary>
/// Difficulty/commitment level
/// </summary>
public enum ReadingPlanDifficulty
{
    Light,      // 5-10 min/day
    Medium,     // 15-20 min/day
    Intensive   // 30+ min/day
}
