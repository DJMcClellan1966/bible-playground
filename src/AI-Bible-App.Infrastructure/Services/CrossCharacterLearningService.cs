using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Service that enables characters to learn and evolve from roundtable discussions.
/// 
/// Key concepts:
/// 1. During roundtables, characters share perspectives
/// 2. This service analyzes what each character can learn from others
/// 3. Insights are stored and used to enhance future responses
/// 4. Characters maintain their core identity but gain new perspectives
/// 
/// Example: When Peter discusses "forgiveness" with Paul and John:
/// - Peter hears Paul's perspective on grace vs law
/// - Peter hears John's perspective on love
/// - Peter synthesizes these into his own evolved understanding
/// - Next time Peter is asked about forgiveness, he incorporates these insights
/// </summary>
public class CrossCharacterLearningService : ICrossCharacterLearningService
{
    private readonly IAIService _aiService;
    private readonly ICharacterRepository _characterRepository;
    private readonly ILogger<CrossCharacterLearningService>? _logger;
    private readonly string _evolutionDirectory;
    
    // In-memory cache of evolving characters
    private readonly Dictionary<string, EvolvingCharacter> _evolvingCache = new();
    private readonly object _cacheLock = new();

    public CrossCharacterLearningService(
        IAIService aiService,
        ICharacterRepository characterRepository,
        ILogger<CrossCharacterLearningService>? logger = null)
    {
        _aiService = aiService;
        _characterRepository = characterRepository;
        _logger = logger;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _evolutionDirectory = Path.Combine(appData, "AI-Bible-App", "CharacterEvolution");
        Directory.CreateDirectory(_evolutionDirectory);
    }

    /// <summary>
    /// Process a roundtable discussion and extract learnings for all participants
    /// </summary>
    public async Task ProcessRoundtableDiscussionAsync(
        string topic,
        List<BiblicalCharacter> participants,
        List<ChatMessage> messages)
    {
        _logger?.LogInformation(
            "Processing roundtable on '{Topic}' with {Count} participants",
            topic, participants.Count);

        // Group messages by character
        var messagesByCharacter = messages
            .Where(m => m.Role == "assistant" && !string.IsNullOrEmpty(m.CharacterId))
            .GroupBy(m => m.CharacterId)
            .ToDictionary(g => g.Key!, g => g.ToList());

        // For each character, analyze what they can learn from others
        foreach (var character in participants)
        {
            var evolvingChar = await GetOrCreateEvolvingCharacterAsync(character);
            var characterMessages = messagesByCharacter.GetValueOrDefault(character.Id) ?? new List<ChatMessage>();
            
            // Get messages from other characters
            var otherMessages = messages
                .Where(m => m.Role == "assistant" && m.CharacterId != character.Id)
                .ToList();

            if (otherMessages.Any())
            {
                await LearnFromOtherCharactersAsync(
                    evolvingChar,
                    character,
                    topic,
                    characterMessages,
                    otherMessages,
                    participants);
            }

            // Update metrics
            evolvingChar.Metrics.TotalRoundtables++;
            evolvingChar.LastEvolvedAt = DateTime.UtcNow;
            evolvingChar.EvolutionVersion++;

            await SaveEvolvingCharacterAsync(evolvingChar);
        }
    }

    /// <summary>
    /// Analyze what one character can learn from others in the discussion
    /// </summary>
    private async Task LearnFromOtherCharactersAsync(
        EvolvingCharacter evolvingChar,
        BiblicalCharacter character,
        string topic,
        List<ChatMessage> myMessages,
        List<ChatMessage> otherMessages,
        List<BiblicalCharacter> allParticipants)
    {
        // Group other messages by character
        var otherCharacterMessages = otherMessages
            .GroupBy(m => m.CharacterId)
            .ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var (otherCharId, theirMessages) in otherCharacterMessages)
        {
            var otherCharacter = allParticipants.FirstOrDefault(p => p.Id == otherCharId);
            if (otherCharacter == null) continue;

            // Get or create cross-character insight
            if (!evolvingChar.CrossCharacterInsights.TryGetValue(otherCharId, out var insight))
            {
                insight = new CrossCharacterInsight
                {
                    SourceCharacterId = otherCharId,
                    SourceCharacterName = otherCharacter.Name,
                    FirstInteraction = DateTime.UtcNow
                };
                evolvingChar.CrossCharacterInsights[otherCharId] = insight;
            }

            // Analyze their messages for learnable content
            await AnalyzeAndLearnAsync(evolvingChar, character, insight, topic, myMessages, theirMessages, otherCharacter);
            
            insight.InteractionCount++;
            insight.LastInteraction = DateTime.UtcNow;
            
            // Update influence score
            insight.InfluenceScore = CalculateInfluenceScore(insight);
        }

        // Synthesize wisdom from the overall discussion
        if (otherMessages.Count >= 2)
        {
            await SynthesizeWisdomAsync(evolvingChar, character, topic, myMessages, otherMessages, allParticipants);
        }
    }

    /// <summary>
    /// Analyze another character's messages and extract learnable insights
    /// </summary>
    private async Task AnalyzeAndLearnAsync(
        EvolvingCharacter evolvingChar,
        BiblicalCharacter myCharacter,
        CrossCharacterInsight insight,
        string topic,
        List<ChatMessage> myMessages,
        List<ChatMessage> theirMessages,
        BiblicalCharacter theirCharacter)
    {
        var myContent = string.Join("\n", myMessages.Select(m => m.Content));
        var theirContent = string.Join("\n", theirMessages.Select(m => m.Content));

        // Use AI to analyze what can be learned
        var analysisPrompt = $@"You are analyzing a discussion between biblical characters to identify learning opportunities.

TOPIC: {topic}

{myCharacter.Name}'s RESPONSES:
{myContent}

{theirCharacter.Name}'s RESPONSES:
{theirContent}

As {myCharacter.Name}, analyze what you could learn from {theirCharacter.Name}'s perspective.

Respond in this exact JSON format:
{{
    ""keyTeachings"": [
        {{
            ""topic"": ""specific topic"",
            ""teaching"": ""what {theirCharacter.Name} taught"",
            ""howItHelps"": ""how this perspective adds to or refines {myCharacter.Name}'s understanding"",
            ""scriptures"": [""any scripture references they used""]
        }}
    ],
    ""pointsOfAgreement"": [""points where both characters agreed""],
    ""productiveDisagreements"": [
        {{
            ""topic"": ""what you disagreed on"",
            ""theirPosition"": ""their view"",
            ""myPosition"": ""{myCharacter.Name}'s view"",
            ""newUnderstanding"": ""any new understanding gained from the disagreement""
        }}
    ],
    ""resonantScriptures"": [""scriptures they used that resonate with {myCharacter.Name}""]
}}

Be specific and thoughtful. Focus on genuine insights that would help {myCharacter.Name} grow.";

        try
        {
            var analysis = await _aiService.GetChatResponseAsync(
                myCharacter,
                new List<ChatMessage>(),
                analysisPrompt);

            // Parse the JSON response
            var result = ParseLearningAnalysis(analysis);
            if (result != null)
            {
                // Add learned teachings
                foreach (var teaching in result.KeyTeachings)
                {
                    var learnedTeaching = new LearnedTeaching
                    {
                        Topic = teaching.Topic,
                        Teaching = teaching.Teaching,
                        Context = topic,
                        HowItChanged = teaching.HowItHelps,
                        SupportingScriptures = teaching.Scriptures ?? new List<string>(),
                        ImpactScore = 0.7,
                        LearnedAt = DateTime.UtcNow
                    };
                    
                    insight.LearnedTeachings.Add(learnedTeaching);
                    evolvingChar.Metrics.TotalTeachingsLearned++;
                    
                    // Also add as a gained perspective
                    evolvingChar.DynamicLayer.GainedPerspectives.Add(new GainedPerspective
                    {
                        Topic = teaching.Topic,
                        Perspective = teaching.Teaching,
                        Source = theirCharacter.Name,
                        Integration = teaching.HowItHelps,
                        GainedAt = DateTime.UtcNow
                    });
                }

                // Add points of agreement
                foreach (var agreement in result.PointsOfAgreement ?? new List<string>())
                {
                    if (!insight.PointsOfAgreement.Contains(agreement))
                    {
                        insight.PointsOfAgreement.Add(agreement);
                    }
                }

                // Add productive disagreements
                foreach (var disagreement in result.ProductiveDisagreements ?? new List<DisagreementResult>())
                {
                    insight.ProductiveDisagreements.Add(new ProductiveDisagreement
                    {
                        Topic = disagreement.Topic,
                        MyOriginalPosition = disagreement.MyPosition,
                        TheirPosition = disagreement.TheirPosition,
                        NewUnderstanding = disagreement.NewUnderstanding,
                        ViewChanged = !string.IsNullOrEmpty(disagreement.NewUnderstanding)
                    });
                }

                // Add resonant scriptures
                foreach (var scripture in result.ResonantScriptures ?? new List<string>())
                {
                    if (!insight.ResonantScriptures.Contains(scripture))
                    {
                        insight.ResonantScriptures.Add(scripture);
                    }
                }

                evolvingChar.Metrics.TotalInsightsGained += result.KeyTeachings?.Count ?? 0;

                // Record growth event if significant learning occurred
                if ((result.KeyTeachings?.Count ?? 0) >= 2)
                {
                    evolvingChar.GrowthEvents.Add(new GrowthEvent
                    {
                        Type = GrowthEventType.NewInsight,
                        Description = $"Gained {result.KeyTeachings!.Count} insights from discussing '{topic}' with {theirCharacter.Name}",
                        TriggerContext = topic,
                        InvolvedCharacters = new List<string> { theirCharacter.Name },
                        ImpactScore = 0.7,
                        OccurredAt = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to analyze learning for {Character} from {Other}",
                myCharacter.Name, theirCharacter.Name);
        }
    }

    /// <summary>
    /// Synthesize overall wisdom from a multi-character discussion
    /// </summary>
    private async Task SynthesizeWisdomAsync(
        EvolvingCharacter evolvingChar,
        BiblicalCharacter character,
        string topic,
        List<ChatMessage> myMessages,
        List<ChatMessage> otherMessages,
        List<BiblicalCharacter> allParticipants)
    {
        var allContent = new System.Text.StringBuilder();
        allContent.AppendLine($"Discussion Topic: {topic}");
        allContent.AppendLine();

        foreach (var msg in myMessages.Concat(otherMessages).OrderBy(m => m.Timestamp))
        {
            var charName = allParticipants.FirstOrDefault(p => p.Id == msg.CharacterId)?.Name ?? "Unknown";
            allContent.AppendLine($"{charName}: {msg.Content}");
            allContent.AppendLine();
        }

        var synthesisPrompt = $@"You are {character.Name}, reflecting on a roundtable discussion about '{topic}'.

THE DISCUSSION:
{allContent}

Based on this discussion with your fellow biblical figures, synthesize the key wisdom that emerged.
As {character.Name}, what is the deeper truth you've come to understand by hearing multiple perspectives?

Respond in this exact JSON format:
{{
    ""synthesizedWisdom"": ""A profound insight that combines perspectives from the discussion"",
    ""contributingPerspectives"": [
        {{
            ""character"": ""character name"",
            ""contribution"": ""what they contributed to this understanding""
        }}
    ],
    ""supportingScriptures"": [""relevant scripture references""],
    ""howThisRefinesMyView"": ""How this discussion has refined your understanding""
}}";

        try
        {
            var synthesis = await _aiService.GetChatResponseAsync(
                character,
                new List<ChatMessage>(),
                synthesisPrompt);

            var result = ParseSynthesisResult(synthesis);
            if (result != null && !string.IsNullOrEmpty(result.SynthesizedWisdom))
            {
                var wisdom = new SynthesizedWisdom
                {
                    Topic = topic,
                    Wisdom = result.SynthesizedWisdom,
                    ContributingSources = result.ContributingPerspectives?.Select(p => p.Character).ToList() ?? new List<string>(),
                    SupportingScriptures = result.SupportingScriptures ?? new List<string>(),
                    DiscussionsContributing = 1,
                    ConfidenceScore = 0.7,
                    SynthesizedAt = DateTime.UtcNow
                };

                evolvingChar.SynthesizedWisdom.Add(wisdom);
                evolvingChar.Metrics.SynthesizedWisdomCount++;

                // Update refined understanding for this topic
                if (!evolvingChar.DynamicLayer.RefinedUnderstandings.ContainsKey(topic))
                {
                    evolvingChar.DynamicLayer.RefinedUnderstandings[topic] = new RefinedUnderstanding
                    {
                        Topic = topic,
                        OriginalView = myMessages.FirstOrDefault()?.Content ?? "",
                        ContributingCharacters = new List<string>()
                    };
                }

                var refined = evolvingChar.DynamicLayer.RefinedUnderstandings[topic];
                refined.EvolvedView = result.SynthesizedWisdom;
                refined.KeyInsights.Add(result.HowThisRefinesMyView ?? "");
                refined.ContributingCharacters = result.ContributingPerspectives?.Select(p => p.Character).Distinct().ToList() ?? new List<string>();
                refined.DiscussionCount++;
                refined.LastRefined = DateTime.UtcNow;

                // Record growth event
                evolvingChar.GrowthEvents.Add(new GrowthEvent
                {
                    Type = GrowthEventType.SynthesizedWisdom,
                    Description = $"Synthesized wisdom on '{topic}' from discussion with {allParticipants.Count - 1} others",
                    TriggerContext = topic,
                    InvolvedCharacters = allParticipants.Where(p => p.Id != character.Id).Select(p => p.Name).ToList(),
                    ImpactScore = 0.8,
                    OccurredAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to synthesize wisdom for {Character}", character.Name);
        }
    }

    /// <summary>
    /// Build an enhanced system prompt that incorporates learned insights
    /// </summary>
    public async Task<string> BuildEvolvedPromptAsync(
        BiblicalCharacter character,
        string basePrompt,
        string currentTopic,
        bool includeLearnedInsights = true)
    {
        var evolvingChar = await GetOrCreateEvolvingCharacterAsync(character);
        var sb = new System.Text.StringBuilder();

        // Start with base prompt
        sb.AppendLine(basePrompt);
        sb.AppendLine();

        if (!includeLearnedInsights || evolvingChar.Metrics.TotalRoundtables == 0)
        {
            return sb.ToString();
        }

        sb.AppendLine("=== EVOLVED UNDERSTANDING ===");
        sb.AppendLine("Through discussions with other biblical figures, you have gained these insights:");
        sb.AppendLine();

        // Add relevant refined understandings
        var relevantUnderstandings = FindRelevantUnderstandings(evolvingChar, currentTopic);
        if (relevantUnderstandings.Any())
        {
            sb.AppendLine("REFINED PERSPECTIVES:");
            foreach (var understanding in relevantUnderstandings.Take(3))
            {
                sb.AppendLine($"- On '{understanding.Topic}': {understanding.EvolvedView}");
                if (understanding.ContributingCharacters.Any())
                {
                    sb.AppendLine($"  (Insight gained with: {string.Join(", ", understanding.ContributingCharacters)})");
                }
            }
            sb.AppendLine();
        }

        // Add relevant cross-character insights
        var relevantInsights = FindRelevantCrossCharacterInsights(evolvingChar, currentTopic);
        if (relevantInsights.Any())
        {
            sb.AppendLine("PERSPECTIVES LEARNED FROM OTHERS:");
            foreach (var (charName, teachings) in relevantInsights.Take(3))
            {
                foreach (var teaching in teachings.Take(2))
                {
                    sb.AppendLine($"- From {charName}: {teaching.Teaching}");
                }
            }
            sb.AppendLine();
        }

        // Add synthesized wisdom
        var relevantWisdom = evolvingChar.SynthesizedWisdom
            .Where(w => IsTopicRelevant(w.Topic, currentTopic))
            .OrderByDescending(w => w.SynthesizedAt)
            .Take(2);

        if (relevantWisdom.Any())
        {
            sb.AppendLine("SYNTHESIZED WISDOM:");
            foreach (var wisdom in relevantWisdom)
            {
                sb.AppendLine($"- {wisdom.Wisdom}");
            }
            sb.AppendLine();
        }

        // Add evolution note
        sb.AppendLine($"(You have participated in {evolvingChar.Metrics.TotalRoundtables} roundtable discussions");
        sb.AppendLine($" and gained {evolvingChar.Metrics.TotalInsightsGained} insights from other characters)");
        sb.AppendLine();
        sb.AppendLine("Use these learned perspectives to enrich your response, but maintain your core identity.");

        return sb.ToString();
    }

    /// <summary>
    /// Get evolution summary for a character
    /// </summary>
    public async Task<CharacterEvolutionSummary> GetEvolutionSummaryAsync(BiblicalCharacter character)
    {
        var evolvingChar = await GetOrCreateEvolvingCharacterAsync(character);
        
        return new CharacterEvolutionSummary
        {
            CharacterName = character.Name,
            TotalRoundtables = evolvingChar.Metrics.TotalRoundtables,
            TotalInsightsGained = evolvingChar.Metrics.TotalInsightsGained,
            TotalTeachingsLearned = evolvingChar.Metrics.TotalTeachingsLearned,
            SynthesizedWisdomCount = evolvingChar.Metrics.SynthesizedWisdomCount,
            CharactersLearnedFrom = evolvingChar.CrossCharacterInsights.Keys.ToList(),
            TopInfluencers = evolvingChar.CrossCharacterInsights
                .OrderByDescending(kv => kv.Value.InfluenceScore)
                .Take(3)
                .Select(kv => new CharacterInfluence
                {
                    CharacterName = kv.Value.SourceCharacterName,
                    InfluenceScore = kv.Value.InfluenceScore,
                    TeachingsLearned = kv.Value.LearnedTeachings.Count
                })
                .ToList(),
            RecentGrowthEvents = evolvingChar.GrowthEvents
                .OrderByDescending(e => e.OccurredAt)
                .Take(5)
                .ToList(),
            EvolutionScore = evolvingChar.DynamicLayer.EvolutionScore,
            LastEvolvedAt = evolvingChar.LastEvolvedAt
        };
    }

    #region Helper Methods

    private async Task<EvolvingCharacter> GetOrCreateEvolvingCharacterAsync(BiblicalCharacter character)
    {
        lock (_cacheLock)
        {
            if (_evolvingCache.TryGetValue(character.Id, out var cached))
            {
                return cached;
            }
        }

        var evolving = await LoadEvolvingCharacterAsync(character.Id);
        
        if (evolving == null)
        {
            evolving = new EvolvingCharacter
            {
                CharacterId = character.Id,
                CharacterName = character.Name,
                StaticCore = new StaticCharacterCore
                {
                    BiblicalFacts = character.BiblicalReferences ?? new List<string>(),
                    BaseSystemPrompt = character.SystemPrompt
                }
            };
            
            _logger?.LogInformation("Created new evolving character for: {Name}", character.Name);
        }

        lock (_cacheLock)
        {
            _evolvingCache[character.Id] = evolving;
        }

        return evolving;
    }

    private async Task<EvolvingCharacter?> LoadEvolvingCharacterAsync(string characterId)
    {
        var filePath = Path.Combine(_evolutionDirectory, $"{characterId}_evolution.json");
        
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<EvolvingCharacter>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load evolving character: {Id}", characterId);
            return null;
        }
    }

    private async Task SaveEvolvingCharacterAsync(EvolvingCharacter evolving)
    {
        var filePath = Path.Combine(_evolutionDirectory, $"{evolving.CharacterId}_evolution.json");
        
        try
        {
            var json = JsonSerializer.Serialize(evolving, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save evolving character: {Id}", evolving.CharacterId);
        }
    }

    private double CalculateInfluenceScore(CrossCharacterInsight insight)
    {
        var teachingScore = Math.Min(insight.LearnedTeachings.Count * 0.1, 0.4);
        var agreementScore = Math.Min(insight.PointsOfAgreement.Count * 0.05, 0.2);
        var disagreementScore = Math.Min(insight.ProductiveDisagreements.Count * 0.08, 0.2);
        var interactionScore = Math.Min(insight.InteractionCount * 0.02, 0.2);
        
        return teachingScore + agreementScore + disagreementScore + interactionScore;
    }

    private List<RefinedUnderstanding> FindRelevantUnderstandings(EvolvingCharacter evolving, string topic)
    {
        return evolving.DynamicLayer.RefinedUnderstandings.Values
            .Where(u => IsTopicRelevant(u.Topic, topic))
            .OrderByDescending(u => u.LastRefined)
            .ToList();
    }

    private Dictionary<string, List<LearnedTeaching>> FindRelevantCrossCharacterInsights(
        EvolvingCharacter evolving, string topic)
    {
        var result = new Dictionary<string, List<LearnedTeaching>>();
        
        foreach (var insight in evolving.CrossCharacterInsights.Values)
        {
            var relevantTeachings = insight.LearnedTeachings
                .Where(t => IsTopicRelevant(t.Topic, topic))
                .ToList();
            
            if (relevantTeachings.Any())
            {
                result[insight.SourceCharacterName] = relevantTeachings;
            }
        }

        return result;
    }

    private bool IsTopicRelevant(string topic1, string topic2)
    {
        if (string.IsNullOrEmpty(topic1) || string.IsNullOrEmpty(topic2))
            return false;

        var words1 = topic1.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = topic2.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Check for keyword overlap
        var spiritualKeywords = new[] { "faith", "love", "grace", "forgiveness", "salvation", "prayer", 
            "god", "jesus", "spirit", "sin", "redemption", "hope", "peace", "wisdom", "truth" };

        var topic1Keywords = words1.Where(w => spiritualKeywords.Contains(w)).ToHashSet();
        var topic2Keywords = words2.Where(w => spiritualKeywords.Contains(w)).ToHashSet();

        return topic1Keywords.Intersect(topic2Keywords).Any() || 
               words1.Intersect(words2).Count() >= 2;
    }

    #region JSON Parsing

    private LearningAnalysisResult? ParseLearningAnalysis(string json)
    {
        try
        {
            // Extract JSON from response (handle if wrapped in markdown)
            var jsonMatch = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (jsonMatch.Success)
            {
                return JsonSerializer.Deserialize<LearningAnalysisResult>(jsonMatch.Value, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse learning analysis JSON");
        }
        return null;
    }

    private SynthesisResult? ParseSynthesisResult(string json)
    {
        try
        {
            var jsonMatch = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (jsonMatch.Success)
            {
                return JsonSerializer.Deserialize<SynthesisResult>(jsonMatch.Value, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse synthesis JSON");
        }
        return null;
    }

    #endregion

    #endregion
}

#region Result Classes

internal class LearningAnalysisResult
{
    public List<TeachingResult>? KeyTeachings { get; set; }
    public List<string>? PointsOfAgreement { get; set; }
    public List<DisagreementResult>? ProductiveDisagreements { get; set; }
    public List<string>? ResonantScriptures { get; set; }
}

internal class TeachingResult
{
    public string Topic { get; set; } = "";
    public string Teaching { get; set; } = "";
    public string HowItHelps { get; set; } = "";
    public List<string>? Scriptures { get; set; }
}

internal class DisagreementResult
{
    public string Topic { get; set; } = "";
    public string TheirPosition { get; set; } = "";
    public string MyPosition { get; set; } = "";
    public string NewUnderstanding { get; set; } = "";
}

internal class SynthesisResult
{
    public string SynthesizedWisdom { get; set; } = "";
    public List<ContributingPerspective>? ContributingPerspectives { get; set; }
    public List<string>? SupportingScriptures { get; set; }
    public string? HowThisRefinesMyView { get; set; }
}

internal class ContributingPerspective
{
    public string Character { get; set; } = "";
    public string Contribution { get; set; } = "";
}

#endregion
