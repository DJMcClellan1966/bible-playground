using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Service that enables biblical characters to learn and evolve from roundtable discussions.
/// Characters maintain their core biblical identity but gain new perspectives from interactions.
/// </summary>
public interface ICrossCharacterLearningService
{
    /// <summary>
    /// Process a completed roundtable discussion to extract learnings for all participants
    /// </summary>
    Task ProcessRoundtableDiscussionAsync(
        string topic,
        List<BiblicalCharacter> participants,
        List<ChatMessage> messages);

    /// <summary>
    /// Build an enhanced system prompt that incorporates the character's learned insights
    /// </summary>
    Task<string> BuildEvolvedPromptAsync(
        BiblicalCharacter character,
        string basePrompt,
        string currentTopic,
        bool includeLearnedInsights = true);

    /// <summary>
    /// Get a summary of a character's evolution journey
    /// </summary>
    Task<CharacterEvolutionSummary> GetEvolutionSummaryAsync(BiblicalCharacter character);
}

/// <summary>
/// Summary of a character's evolution for display
/// </summary>
public class CharacterEvolutionSummary
{
    public string CharacterName { get; set; } = "";
    public int TotalRoundtables { get; set; }
    public int TotalInsightsGained { get; set; }
    public int TotalTeachingsLearned { get; set; }
    public int SynthesizedWisdomCount { get; set; }
    public List<string> CharactersLearnedFrom { get; set; } = new();
    public List<CharacterInfluence> TopInfluencers { get; set; } = new();
    public List<GrowthEvent> RecentGrowthEvents { get; set; } = new();
    public double EvolutionScore { get; set; }
    public DateTime LastEvolvedAt { get; set; }
}

public class CharacterInfluence
{
    public string CharacterName { get; set; } = "";
    public double InfluenceScore { get; set; }
    public int TeachingsLearned { get; set; }
}
