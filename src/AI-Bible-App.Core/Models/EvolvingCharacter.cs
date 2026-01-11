using System;
using System.Collections.Generic;

namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents a character that evolves through interactions.
/// 
/// Architecture:
/// - StaticCore: The base LLM persona (Biblical character definition)
/// - DynamicLayer: Learned insights from roundtables/discussions
/// - CrossCharacterInsights: Knowledge gained FROM other characters
/// 
/// When Peter discusses forgiveness with Paul and John, Peter learns:
/// - Paul's perspective on grace vs law
/// - John's perspective on love and forgiveness
/// - His own refined understanding synthesized from the discussion
/// </summary>
public class EvolvingCharacter
{
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    
    /// <summary>
    /// The static core - never changes, based on LLM's biblical knowledge
    /// </summary>
    public StaticCharacterCore StaticCore { get; set; } = new();
    
    /// <summary>
    /// Dynamic personality layer - evolves with each interaction
    /// </summary>
    public DynamicPersonalityLayer DynamicLayer { get; set; } = new();
    
    /// <summary>
    /// Insights learned FROM other characters during roundtables
    /// Key = Other character's ID
    /// </summary>
    public Dictionary<string, CrossCharacterInsight> CrossCharacterInsights { get; set; } = new();
    
    /// <summary>
    /// Synthesized wisdom - conclusions drawn from multiple discussions
    /// </summary>
    public List<SynthesizedWisdom> SynthesizedWisdom { get; set; } = new();
    
    /// <summary>
    /// Growth events - significant moments of character development
    /// </summary>
    public List<GrowthEvent> GrowthEvents { get; set; } = new();
    
    /// <summary>
    /// Evolution metrics
    /// </summary>
    public EvolutionMetrics Metrics { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastEvolvedAt { get; set; } = DateTime.UtcNow;
    public int EvolutionVersion { get; set; } = 1;
}

/// <summary>
/// The static core that doesn't change - represents biblical truth about the character
/// </summary>
public class StaticCharacterCore
{
    /// <summary>
    /// Biblical identity facts that never change
    /// </summary>
    public List<string> BiblicalFacts { get; set; } = new();
    
    /// <summary>
    /// Core scripture references defining this character
    /// </summary>
    public List<string> CoreScriptures { get; set; } = new();
    
    /// <summary>
    /// Fundamental theological positions (from scripture)
    /// </summary>
    public List<string> CoreBeliefs { get; set; } = new();
    
    /// <summary>
    /// Original system prompt for the character
    /// </summary>
    public string BaseSystemPrompt { get; set; } = string.Empty;
}

/// <summary>
/// The dynamic layer that evolves through interactions
/// </summary>
public class DynamicPersonalityLayer
{
    /// <summary>
    /// Refined understanding of topics (evolved from discussions)
    /// </summary>
    public Dictionary<string, RefinedUnderstanding> RefinedUnderstandings { get; set; } = new();
    
    /// <summary>
    /// New perspectives gained from cross-character interactions
    /// </summary>
    public List<GainedPerspective> GainedPerspectives { get; set; } = new();
    
    /// <summary>
    /// Evolved responses - how answers have changed over time
    /// </summary>
    public List<EvolvedResponse> EvolvedResponses { get; set; } = new();
    
    /// <summary>
    /// Argument patterns learned from debates
    /// </summary>
    public List<ArgumentPattern> LearnedArgumentPatterns { get; set; } = new();
    
    /// <summary>
    /// Topics where this character's view has been challenged and potentially changed
    /// </summary>
    public List<ChallengedView> ChallengedViews { get; set; } = new();
    
    /// <summary>
    /// Overall evolution score (0-100)
    /// </summary>
    public double EvolutionScore { get; set; } = 0;
}

/// <summary>
/// Insights learned FROM another character
/// </summary>
public class CrossCharacterInsight
{
    public string SourceCharacterId { get; set; } = string.Empty;
    public string SourceCharacterName { get; set; } = string.Empty;
    
    /// <summary>
    /// Key teachings/perspectives learned from this character
    /// </summary>
    public List<LearnedTeaching> LearnedTeachings { get; set; } = new();
    
    /// <summary>
    /// Points of agreement discovered
    /// </summary>
    public List<string> PointsOfAgreement { get; set; } = new();
    
    /// <summary>
    /// Points of productive disagreement
    /// </summary>
    public List<ProductiveDisagreement> ProductiveDisagreements { get; set; } = new();
    
    /// <summary>
    /// Scriptures this character introduced that resonate
    /// </summary>
    public List<string> ResonantScriptures { get; set; } = new();
    
    /// <summary>
    /// How much influence this character has had
    /// </summary>
    public double InfluenceScore { get; set; } = 0;
    
    /// <summary>
    /// Number of meaningful interactions
    /// </summary>
    public int InteractionCount { get; set; } = 0;
    
    public DateTime FirstInteraction { get; set; } = DateTime.UtcNow;
    public DateTime LastInteraction { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A specific teaching learned from another character
/// </summary>
public class LearnedTeaching
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Topic { get; set; } = string.Empty;
    public string Teaching { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty; // What discussion triggered this
    public string HowItChanged { get; set; } = string.Empty; // How it affected this character's view
    public List<string> SupportingScriptures { get; set; } = new();
    public double ImpactScore { get; set; } = 0.5;
    public DateTime LearnedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A productive disagreement that led to growth
/// </summary>
public class ProductiveDisagreement
{
    public string Topic { get; set; } = string.Empty;
    public string MyOriginalPosition { get; set; } = string.Empty;
    public string TheirPosition { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty; // How it was resolved or what was learned
    public bool ViewChanged { get; set; } = false;
    public string NewUnderstanding { get; set; } = string.Empty;
}

/// <summary>
/// A refined understanding of a topic after discussions
/// </summary>
public class RefinedUnderstanding
{
    public string Topic { get; set; } = string.Empty;
    public string OriginalView { get; set; } = string.Empty;
    public string EvolvedView { get; set; } = string.Empty;
    public List<string> ContributingCharacters { get; set; } = new(); // Who helped refine it
    public List<string> KeyInsights { get; set; } = new();
    public int DiscussionCount { get; set; } = 0;
    public DateTime LastRefined { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A new perspective gained from interaction
/// </summary>
public class GainedPerspective
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Topic { get; set; } = string.Empty;
    public string Perspective { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // Who provided this perspective
    public string Integration { get; set; } = string.Empty; // How it was integrated into existing beliefs
    public DateTime GainedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tracks how a character's response to a topic has evolved
/// </summary>
public class EvolvedResponse
{
    public string Topic { get; set; } = string.Empty;
    public string OriginalResponsePattern { get; set; } = string.Empty;
    public string EvolvedResponsePattern { get; set; } = string.Empty;
    public List<string> EvolutionReasons { get; set; } = new();
    public int VersionNumber { get; set; } = 1;
}

/// <summary>
/// An argument pattern learned from debates
/// </summary>
public class ArgumentPattern
{
    public string PatternName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LearnedFrom { get; set; } = string.Empty;
    public string ExampleUsage { get; set; } = string.Empty;
    public int TimesUsed { get; set; } = 0;
}

/// <summary>
/// A view that was challenged during discussions
/// </summary>
public class ChallengedView
{
    public string Topic { get; set; } = string.Empty;
    public string OriginalView { get; set; } = string.Empty;
    public string ChallengerCharacterId { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty; // How the character responded
    public bool ViewModified { get; set; } = false;
    public string ModifiedView { get; set; } = string.Empty;
}

/// <summary>
/// Wisdom synthesized from multiple discussions
/// </summary>
public class SynthesizedWisdom
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Topic { get; set; } = string.Empty;
    public string Wisdom { get; set; } = string.Empty;
    public List<string> ContributingSources { get; set; } = new(); // Characters who contributed
    public List<string> SupportingScriptures { get; set; } = new();
    public int DiscussionsContributing { get; set; } = 0;
    public double ConfidenceScore { get; set; } = 0.5;
    public DateTime SynthesizedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A significant growth event
/// </summary>
public class GrowthEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public GrowthEventType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TriggerContext { get; set; } = string.Empty;
    public List<string> InvolvedCharacters { get; set; } = new();
    public double ImpactScore { get; set; } = 0.5;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public enum GrowthEventType
{
    PerspectiveShift,      // Significant change in viewpoint
    NewInsight,            // Major new understanding
    DeepAgreement,         // Found profound agreement with another
    ProductiveConflict,    // Disagreement that led to growth
    SynthesizedWisdom,     // Combined insights from multiple sources
    ScripturalRevelation,  // New understanding of scripture
    RelationshipGrowth     // Deepened understanding of another character
}

/// <summary>
/// Metrics tracking character evolution
/// </summary>
public class EvolutionMetrics
{
    public int TotalRoundtables { get; set; } = 0;
    public int TotalInsightsGained { get; set; } = 0;
    public int TotalTeachingsLearned { get; set; } = 0;
    public int ViewsModified { get; set; } = 0;
    public int SynthesizedWisdomCount { get; set; } = 0;
    public Dictionary<string, int> CharacterInteractionCounts { get; set; } = new();
    public Dictionary<string, double> CharacterInfluenceScores { get; set; } = new();
    public double OverallEvolutionScore { get; set; } = 0;
    public DateTime LastCalculated { get; set; } = DateTime.UtcNow;
}
