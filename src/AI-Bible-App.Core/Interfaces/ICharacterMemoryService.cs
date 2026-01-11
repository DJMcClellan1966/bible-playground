using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Service for managing what biblical characters "remember" about users.
/// Enables personalized conversations without per-user model training.
/// </summary>
public interface ICharacterMemoryService
{
    /// <summary>
    /// Gets what a character remembers about a specific user
    /// </summary>
    Task<UserCharacterMemory?> GetMemoryAsync(string userId, string characterId);
    
    /// <summary>
    /// Gets all character memories for a user (for backup/export)
    /// </summary>
    Task<List<UserCharacterMemory>> GetAllMemoriesForUserAsync(string userId);
    
    /// <summary>
    /// Saves/updates a character's memory of a user
    /// </summary>
    Task SaveMemoryAsync(UserCharacterMemory memory);
    
    /// <summary>
    /// Records a new interaction, updating relevant memory fields
    /// </summary>
    Task RecordInteractionAsync(string userId, string characterId, string userMessage, string characterResponse);
    
    /// <summary>
    /// Extracts insights from a conversation and updates memory
    /// </summary>
    Task<ConversationInsights> ExtractAndStoreInsightsAsync(
        string userId, 
        string characterId, 
        List<ChatMessage> conversation);
    
    /// <summary>
    /// Gets a context summary for prompt injection
    /// </summary>
    Task<string> GetContextForPromptAsync(string userId, string characterId);
    
    /// <summary>
    /// Marks a life situation as resolved
    /// </summary>
    Task MarkSituationResolvedAsync(string userId, string characterId, string situationId, string? resolution);
    
    /// <summary>
    /// Records a scripture that resonated with the user
    /// </summary>
    Task RecordResonantScriptureAsync(string userId, string characterId, string reference, string? reason);
    
    /// <summary>
    /// Clears all memory for a user (privacy request)
    /// </summary>
    Task ClearUserMemoryAsync(string userId);
    
    /// <summary>
    /// Exports user memory for portability
    /// </summary>
    Task<string> ExportUserMemoryAsync(string userId);
}

/// <summary>
/// Insights extracted from a conversation
/// </summary>
public class ConversationInsights
{
    public List<string> DetectedEmotions { get; set; } = new();
    public List<string> DiscussedTopics { get; set; } = new();
    public List<string> MentionedSituations { get; set; } = new();
    public List<string> ReferencedScriptures { get; set; } = new();
    public bool? WasBreakthroughMoment { get; set; }
    public string? BreakthroughSummary { get; set; }
    public CommunicationPreferences? InferredPreferences { get; set; }
}
