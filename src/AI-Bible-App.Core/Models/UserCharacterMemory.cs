using System.Text.Json.Serialization;

namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents what a biblical character has "learned" about a specific user over time.
/// This enables personalized, contextual conversations without requiring per-user model training.
/// </summary>
public class UserCharacterMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The user this memory belongs to
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// The character who holds this memory of the user
    /// </summary>
    public string CharacterId { get; set; } = string.Empty;
    
    /// <summary>
    /// When this memory relationship was first created
    /// </summary>
    public DateTime FirstInteraction { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Most recent interaction
    /// </summary>
    public DateTime LastInteraction { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Total number of conversations with this character
    /// </summary>
    public int ConversationCount { get; set; } = 0;
    
    /// <summary>
    /// Key life situations the user has shared (job loss, marriage struggles, etc.)
    /// </summary>
    public List<UserLifeSituation> LifeSituations { get; set; } = new();
    
    /// <summary>
    /// Emotional patterns observed in conversations
    /// </summary>
    public List<EmotionalInsight> EmotionalPatterns { get; set; } = new();
    
    /// <summary>
    /// Topics the user frequently discusses or returns to
    /// </summary>
    public List<RecurringTopic> RecurringTopics { get; set; } = new();
    
    /// <summary>
    /// Scripture passages that resonated with the user (they asked to save, discussed further, etc.)
    /// </summary>
    public List<ResonantScripture> ResonantScriptures { get; set; } = new();
    
    /// <summary>
    /// Personal details the user has shared (name, family situation, struggles)
    /// </summary>
    public UserPersonalContext PersonalContext { get; set; } = new();
    
    /// <summary>
    /// How the character should approach this user based on learned preferences
    /// </summary>
    public CommunicationPreferences CommunicationStyle { get; set; } = new();
    
    /// <summary>
    /// Key moments or breakthroughs in the relationship
    /// </summary>
    public List<SignificantMoment> SignificantMoments { get; set; } = new();
    
    /// <summary>
    /// Generates a context summary for injection into prompts
    /// </summary>
    public string GenerateContextSummary()
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(PersonalContext.PreferredName))
            parts.Add($"This person prefers to be called {PersonalContext.PreferredName}.");
        
        if (PersonalContext.FamilySituation != null)
            parts.Add($"Family: {PersonalContext.FamilySituation}");
        
        if (LifeSituations.Any())
        {
            var recent = LifeSituations.OrderByDescending(s => s.LastMentioned).Take(2);
            parts.Add($"Currently dealing with: {string.Join(", ", recent.Select(s => s.Summary))}");
        }
        
        if (RecurringTopics.Any())
        {
            var topTopics = RecurringTopics.OrderByDescending(t => t.MentionCount).Take(3);
            parts.Add($"Often discusses: {string.Join(", ", topTopics.Select(t => t.Topic))}");
        }
        
        if (ResonantScriptures.Any())
        {
            var favorites = ResonantScriptures.OrderByDescending(s => s.TimesReferenced).Take(2);
            parts.Add($"Scriptures that spoke to them: {string.Join(", ", favorites.Select(s => s.Reference))}");
        }
        
        if (EmotionalPatterns.Any())
        {
            var dominant = EmotionalPatterns.OrderByDescending(e => e.Frequency).FirstOrDefault();
            if (dominant != null)
                parts.Add($"Often expresses: {dominant.Emotion}");
        }
        
        if (CommunicationStyle.PrefersDirectness.HasValue)
        {
            parts.Add(CommunicationStyle.PrefersDirectness.Value 
                ? "Appreciates direct, practical responses" 
                : "Appreciates gentle, exploratory conversations");
        }
        
        if (SignificantMoments.Any())
        {
            var recent = SignificantMoments.OrderByDescending(m => m.Timestamp).FirstOrDefault();
            if (recent != null && (DateTime.UtcNow - recent.Timestamp).TotalDays < 30)
                parts.Add($"Recent meaningful moment: {recent.Summary}");
        }
        
        return parts.Any() 
            ? $"WHAT I KNOW ABOUT THIS PERSON:\n{string.Join("\n", parts.Select(p => $"- {p}"))}"
            : string.Empty;
    }
}

/// <summary>
/// A life situation the user has shared
/// </summary>
public class UserLifeSituation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "work", "family", "health", "faith", "relationship"
    public DateTime FirstMentioned { get; set; } = DateTime.UtcNow;
    public DateTime LastMentioned { get; set; } = DateTime.UtcNow;
    public int MentionCount { get; set; } = 1;
    public bool IsResolved { get; set; } = false;
    public string? Resolution { get; set; }
}

/// <summary>
/// Emotional patterns observed in conversations
/// </summary>
public class EmotionalInsight
{
    public string Emotion { get; set; } = string.Empty; // "anxiety", "grief", "hope", "doubt", "joy"
    public int Frequency { get; set; } = 1;
    public List<string> Triggers { get; set; } = new(); // What topics trigger this emotion
    public DateTime LastObserved { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Topics the user returns to frequently
/// </summary>
public class RecurringTopic
{
    public string Topic { get; set; } = string.Empty;
    public int MentionCount { get; set; } = 1;
    public DateTime LastMentioned { get; set; } = DateTime.UtcNow;
    public List<string> RelatedScriptures { get; set; } = new();
}

/// <summary>
/// Scripture that resonated with the user
/// </summary>
public class ResonantScripture
{
    public string Reference { get; set; } = string.Empty;
    public string? WhyItResonated { get; set; }
    public DateTime FirstShared { get; set; } = DateTime.UtcNow;
    public int TimesReferenced { get; set; } = 1;
}

/// <summary>
/// Personal context the user has shared
/// </summary>
public class UserPersonalContext
{
    public string? PreferredName { get; set; }
    public string? FamilySituation { get; set; } // "married with 2 kids", "single parent", etc.
    public string? LifeStage { get; set; } // "young adult", "new parent", "empty nester", "retired"
    public string? FaithBackground { get; set; } // "lifelong Christian", "new believer", "exploring"
    public List<string> MentionedStruggles { get; set; } = new();
    public List<string> MentionedJoys { get; set; } = new();
}

/// <summary>
/// How the character should communicate with this user
/// </summary>
public class CommunicationPreferences
{
    public bool? PrefersDirectness { get; set; }
    public bool? AppreciatesHumor { get; set; }
    public bool? WantsScriptureReferences { get; set; }
    public bool? PrefersQuestions { get; set; } // Does user like being asked questions?
    public bool? NeedsEncouragement { get; set; }
    public string? PreferredResponseLength { get; set; } // "brief", "detailed", "conversational"
}

/// <summary>
/// A significant moment in the character-user relationship
/// </summary>
public class SignificantMoment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public string? UserMessage { get; set; }
    public string? CharacterResponse { get; set; }
    public string? WhySignificant { get; set; } // "breakthrough", "deep sharing", "resolved struggle"
}
