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
/// Service that manages character-specific "intelligence" - the evolving memory and personality
/// that grows from each interaction while the base LLM stays static.
/// 
/// Think of it as: Base LLM + Character Intelligence = Personalized Character Response
/// </summary>
public class CharacterIntelligenceService
{
    private readonly ILogger<CharacterIntelligenceService>? _logger;
    private readonly IAIService _aiService;
    private readonly string _intelligenceDirectory;
    
    // In-memory cache of character intelligences
    private readonly Dictionary<string, CharacterIntelligence> _intelligenceCache = new();
    
    // Lock for thread safety
    private readonly object _cacheLock = new();

    public CharacterIntelligenceService(
        IAIService aiService,
        ILogger<CharacterIntelligenceService>? logger = null)
    {
        _aiService = aiService;
        _logger = logger;
        
        // Store character intelligences in app data
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _intelligenceDirectory = Path.Combine(appData, "AI-Bible-App", "CharacterIntelligence");
        Directory.CreateDirectory(_intelligenceDirectory);
    }

    /// <summary>
    /// Get or create intelligence for a character
    /// </summary>
    public async Task<CharacterIntelligence> GetOrCreateIntelligenceAsync(BiblicalCharacter character)
    {
        lock (_cacheLock)
        {
            if (_intelligenceCache.TryGetValue(character.Id, out var cached))
            {
                return cached;
            }
        }

        // Try to load from disk
        var intelligence = await LoadIntelligenceAsync(character.Id);
        
        if (intelligence == null)
        {
            // Create new intelligence for this character
            intelligence = new CharacterIntelligence
            {
                CharacterId = character.Id,
                CharacterName = character.Name,
                Profile = InitializeProfileFromCharacter(character)
            };
            
            _logger?.LogInformation("Created new intelligence for character: {CharacterName}", character.Name);
        }

        lock (_cacheLock)
        {
            _intelligenceCache[character.Id] = intelligence;
        }

        return intelligence;
    }

    /// <summary>
    /// Record an interaction and update the character's intelligence
    /// </summary>
    public async Task RecordInteractionAsync(
        BiblicalCharacter character,
        MemoryType type,
        string context,
        string response,
        string userInput,
        List<string>? otherParticipants = null)
    {
        var intelligence = await GetOrCreateIntelligenceAsync(character);
        
        // Create the memory
        var memory = new CharacterMemory
        {
            Type = type,
            Context = context,
            Response = response,
            UserInput = userInput,
            OtherParticipants = otherParticipants ?? new List<string>(),
            Timestamp = DateTime.UtcNow
        };

        // Extract claims and scriptures from the response
        memory.ExtractedClaims = ExtractClaims(response);
        memory.ScripturesUsed = ExtractScriptures(response);
        memory.EmotionalTone = AnalyzeEmotionalTone(response);
        memory.Importance = CalculateImportance(memory);

        // Add to memories
        intelligence.Memories.Add(memory);

        // Update stats
        UpdateStats(intelligence, type, response, context);

        // Update profile based on new memory
        await UpdateProfileFromMemoryAsync(intelligence, memory);

        // Update topic stances
        UpdateTopicStances(intelligence, memory);

        // Update relationships if roundtable
        if (otherParticipants?.Any() == true)
        {
            UpdateRelationships(intelligence, memory);
        }

        intelligence.LastUpdatedAt = DateTime.UtcNow;
        intelligence.Version++;

        // Save to disk
        await SaveIntelligenceAsync(intelligence);

        _logger?.LogDebug(
            "Recorded {Type} interaction for {Character}. Total memories: {Count}",
            type, character.Name, intelligence.Memories.Count);
    }

    /// <summary>
    /// Build a synthesized prompt that incorporates the character's learned intelligence
    /// </summary>
    public async Task<string> SynthesizePromptAsync(
        BiblicalCharacter character,
        string basePrompt,
        string currentContext,
        int maxRelevantMemories = 5)
    {
        var intelligence = await GetOrCreateIntelligenceAsync(character);
        
        var sb = new System.Text.StringBuilder();

        // Start with evolved description if we have one
        if (!string.IsNullOrEmpty(intelligence.Profile.EvolvedDescription))
        {
            sb.AppendLine("=== CHARACTER INTELLIGENCE ===");
            sb.AppendLine(intelligence.Profile.EvolvedDescription);
            sb.AppendLine();
        }

        // Add learned traits
        if (intelligence.LearnedTraits.Any())
        {
            sb.AppendLine("=== LEARNED TRAITS ===");
            foreach (var trait in intelligence.LearnedTraits
                .OrderByDescending(t => t.Confidence)
                .Take(5))
            {
                sb.AppendLine($"- {trait.Trait} (observed {trait.OccurrenceCount} times)");
            }
            sb.AppendLine();
        }

        // Add relevant topic stances
        var relevantStances = FindRelevantStances(intelligence, currentContext);
        if (relevantStances.Any())
        {
            sb.AppendLine("=== YOUR KNOWN POSITIONS ===");
            foreach (var stance in relevantStances.Take(3))
            {
                sb.AppendLine($"On {stance.Topic}: {stance.Position}");
            }
            sb.AppendLine();
        }

        // Add relevant memories
        var relevantMemories = await FindRelevantMemoriesAsync(intelligence, currentContext, maxRelevantMemories);
        if (relevantMemories.Any())
        {
            sb.AppendLine("=== TOPICS YOU'VE DISCUSSED BEFORE ===");
            sb.AppendLine("(You have addressed similar questions before. AVOID repeating the same stories or examples. Use DIFFERENT anecdotes from your life.)");
            foreach (var mem in relevantMemories)
            {
                sb.AppendLine($"- Previously discussed: \"{TruncateString(mem.Context, 50)}\" - Find a NEW angle or story to share this time.");
            }
            sb.AppendLine();
        }

        // Add communication style guidance
        var style = intelligence.Profile.CommunicationStyle;
        if (intelligence.Stats.TotalInteractions > 5) // Only after some interactions
        {
            sb.AppendLine("=== YOUR COMMUNICATION STYLE ===");
            if (style.Formality > 0.7) sb.AppendLine("- You tend to speak formally");
            else if (style.Formality < 0.3) sb.AppendLine("- You tend to speak casually");
            
            if (style.StorytellingTendency > 0.6) sb.AppendLine("- You often use stories and parables");
            if (style.QuestionAsking > 0.6) sb.AppendLine("- You often ask thought-provoking questions");
            if (style.DirectionFocus > 0.7) sb.AppendLine("- You speak directly and clearly");
            
            if (intelligence.Profile.SignaturePhrases.Any())
            {
                sb.AppendLine($"- Common phrases you use: {string.Join(", ", intelligence.Profile.SignaturePhrases.Take(3))}");
            }
            sb.AppendLine();
        }

        // Add favorite scriptures
        if (intelligence.Profile.FavoriteScriptures.Any())
        {
            var topScriptures = intelligence.Profile.FavoriteScriptures
                .OrderByDescending(s => s.TimesUsed)
                .Take(3)
                .Select(s => s.Reference);
            sb.AppendLine($"=== SCRIPTURES YOU OFTEN CITE ===");
            sb.AppendLine(string.Join(", ", topScriptures));
            sb.AppendLine();
        }

        // Add the base prompt
        sb.AppendLine("=== CURRENT TASK ===");
        sb.AppendLine(basePrompt);

        return sb.ToString();
    }

    /// <summary>
    /// Analyze the character's intelligence and rebuild the profile
    /// Called periodically to synthesize all memories into a coherent profile
    /// </summary>
    public async Task RebuildProfileAsync(BiblicalCharacter character)
    {
        var intelligence = await GetOrCreateIntelligenceAsync(character);
        
        if (intelligence.Memories.Count < 3)
        {
            _logger?.LogDebug("Not enough memories to rebuild profile for {Character}", character.Name);
            return;
        }

        _logger?.LogInformation("Rebuilding profile for {Character} from {Count} memories", 
            character.Name, intelligence.Memories.Count);

        // Use the AI to analyze the character's past responses and generate an evolved description
        var analysisPrompt = BuildProfileAnalysisPrompt(intelligence);
        
        try
        {
            var analysis = await _aiService.GetChatResponseAsync(
                character,
                new List<ChatMessage>(),
                analysisPrompt);

            if (!string.IsNullOrWhiteSpace(analysis))
            {
                intelligence.Profile.EvolvedDescription = analysis;
                intelligence.Profile.ProfileConfidence = 
                    Math.Min(1.0, intelligence.Memories.Count / 20.0); // Max confidence at 20 memories
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate profile analysis for {Character}", character.Name);
        }

        // Analyze communication patterns
        AnalyzeCommunicationPatterns(intelligence);

        // Extract signature phrases
        ExtractSignaturePhrases(intelligence);

        intelligence.LastUpdatedAt = DateTime.UtcNow;
        await SaveIntelligenceAsync(intelligence);
    }

    /// <summary>
    /// Get a summary of a character's intelligence
    /// </summary>
    public async Task<string> GetIntelligenceSummaryAsync(BiblicalCharacter character)
    {
        var intelligence = await GetOrCreateIntelligenceAsync(character);
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== {character.Name}'s Intelligence Profile ===");
        sb.AppendLine($"Version: {intelligence.Version}");
        sb.AppendLine($"Total Interactions: {intelligence.Stats.TotalInteractions}");
        sb.AppendLine($"  - Chats: {intelligence.Stats.ChatCount}");
        sb.AppendLine($"  - Prayers: {intelligence.Stats.PrayerCount}");
        sb.AppendLine($"  - Roundtables: {intelligence.Stats.RoundtableCount}");
        sb.AppendLine($"Total Memories: {intelligence.Memories.Count}");
        sb.AppendLine($"Learned Traits: {intelligence.LearnedTraits.Count}");
        sb.AppendLine($"Topic Stances: {intelligence.TopicStances.Count}");
        sb.AppendLine($"Relationships: {intelligence.Relationships.Count}");
        sb.AppendLine($"Profile Confidence: {intelligence.Profile.ProfileConfidence:P0}");
        
        if (!string.IsNullOrEmpty(intelligence.Profile.EvolvedDescription))
        {
            sb.AppendLine();
            sb.AppendLine("Evolved Description:");
            sb.AppendLine(intelligence.Profile.EvolvedDescription);
        }

        return sb.ToString();
    }

    #region Private Helper Methods

    private CharacterProfile InitializeProfileFromCharacter(BiblicalCharacter character)
    {
        return new CharacterProfile
        {
            PersonalityTraits = new List<string> { character.PrimaryTone.ToString() },
            PreferredTopics = character.BiblicalReferences?.ToList() ?? new List<string>(),
            EvolvedDescription = character.Description ?? ""
        };
    }

    private List<string> ExtractClaims(string response)
    {
        var claims = new List<string>();
        
        // Split into sentences and take key statements
        var sentences = response.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            // Look for declarative statements
            if (trimmed.Length > 20 && trimmed.Length < 200)
            {
                // Check for claim-like patterns
                if (ContainsClaimPattern(trimmed))
                {
                    claims.Add(trimmed);
                }
            }
        }

        return claims.Take(5).ToList();
    }

    private bool ContainsClaimPattern(string text)
    {
        var patterns = new[]
        {
            @"\b(believe|think|know|understand|see|feel)\b",
            @"\b(is|are|was|were)\s+(the|a|an)\b",
            @"\b(must|should|ought|need)\b",
            @"\b(truth|faith|love|grace|salvation|redemption)\b",
            @"\b(God|Lord|Christ|Spirit)\b"
        };

        return patterns.Any(p => Regex.IsMatch(text, p, RegexOptions.IgnoreCase));
    }

    private List<string> ExtractScriptures(string response)
    {
        var scriptures = new List<string>();
        
        // Pattern to match scripture references like "John 3:16" or "Genesis 1:1-3"
        var pattern = @"\b([1-3]?\s?[A-Za-z]+)\s+(\d+):(\d+)(?:-(\d+))?\b";
        var matches = Regex.Matches(response, pattern);
        
        foreach (Match match in matches)
        {
            scriptures.Add(match.Value);
        }

        return scriptures.Distinct().ToList();
    }

    private double AnalyzeEmotionalTone(string response)
    {
        var positiveWords = new[] { "joy", "love", "peace", "hope", "grace", "blessing", "wonderful", "beautiful", "praise", "thank" };
        var negativeWords = new[] { "sin", "sorrow", "suffering", "pain", "darkness", "fear", "judgment", "wrath", "death", "lost" };

        var words = response.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var positiveCount = words.Count(w => positiveWords.Any(p => w.Contains(p)));
        var negativeCount = words.Count(w => negativeWords.Any(n => w.Contains(n)));

        var total = positiveCount + negativeCount;
        if (total == 0) return 0.5;

        return (double)positiveCount / total;
    }

    private double CalculateImportance(CharacterMemory memory)
    {
        double importance = 0.5;

        // More claims = more important
        importance += memory.ExtractedClaims.Count * 0.05;

        // Scripture references add importance
        importance += memory.ScripturesUsed.Count * 0.1;

        // Longer responses might be more substantive
        if (memory.Response.Length > 500) importance += 0.1;

        // Roundtables and debates are more significant
        if (memory.Type == MemoryType.Roundtable || memory.Type == MemoryType.Debate)
            importance += 0.15;

        return Math.Min(1.0, importance);
    }

    private void UpdateStats(CharacterIntelligence intelligence, MemoryType type, string response, string context)
    {
        intelligence.Stats.TotalInteractions++;
        intelligence.Stats.TotalWordsGenerated += response.Split(' ').Length;
        intelligence.Stats.LastInteraction = DateTime.UtcNow;

        switch (type)
        {
            case MemoryType.Chat:
                intelligence.Stats.ChatCount++;
                break;
            case MemoryType.Prayer:
                intelligence.Stats.PrayerCount++;
                break;
            case MemoryType.Roundtable:
            case MemoryType.Debate:
                intelligence.Stats.RoundtableCount++;
                break;
            case MemoryType.WisdomCouncil:
                intelligence.Stats.WisdomCouncilCount++;
                break;
        }

        // Track topic frequency
        var topic = ExtractMainTopic(context);
        if (!string.IsNullOrEmpty(topic))
        {
            if (intelligence.Stats.TopicFrequency.ContainsKey(topic))
                intelligence.Stats.TopicFrequency[topic]++;
            else
                intelligence.Stats.TopicFrequency[topic] = 1;
            
            intelligence.Stats.UniqueTopicsDiscussed = intelligence.Stats.TopicFrequency.Count;
        }
    }

    private string ExtractMainTopic(string context)
    {
        // Simple topic extraction - take first few meaningful words
        var words = context.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Where(w => !new[] { "what", "about", "tell", "think", "does", "would", "could", "should", "have", "been", "from", "that", "this", "with" }.Contains(w))
            .Take(3);
        
        return string.Join(" ", words);
    }

    private async Task UpdateProfileFromMemoryAsync(CharacterIntelligence intelligence, CharacterMemory memory)
    {
        // Update favorite scriptures
        foreach (var scripture in memory.ScripturesUsed)
        {
            var existing = intelligence.Profile.FavoriteScriptures.FirstOrDefault(s => s.Reference == scripture);
            if (existing != null)
            {
                existing.TimesUsed++;
                existing.ContextsUsed.Add(TruncateString(memory.Context, 50));
            }
            else
            {
                intelligence.Profile.FavoriteScriptures.Add(new ScripturePreference
                {
                    Reference = scripture,
                    TimesUsed = 1,
                    ContextsUsed = new List<string> { TruncateString(memory.Context, 50) }
                });
            }
        }

        // Extract and update traits from claims
        foreach (var claim in memory.ExtractedClaims)
        {
            var trait = InferTraitFromClaim(claim);
            if (!string.IsNullOrEmpty(trait))
            {
                var existingTrait = intelligence.LearnedTraits.FirstOrDefault(t => 
                    t.Trait.Equals(trait, StringComparison.OrdinalIgnoreCase));
                
                if (existingTrait != null)
                {
                    existingTrait.OccurrenceCount++;
                    existingTrait.LastObserved = DateTime.UtcNow;
                    existingTrait.Confidence = Math.Min(1.0, existingTrait.Confidence + 0.05);
                }
                else
                {
                    intelligence.LearnedTraits.Add(new LearnedTrait
                    {
                        Trait = trait,
                        Evidence = TruncateString(claim, 100),
                        Confidence = 0.3
                    });
                }
            }
        }

        await Task.CompletedTask;
    }

    private string? InferTraitFromClaim(string claim)
    {
        var lowerClaim = claim.ToLower();

        if (lowerClaim.Contains("faith") || lowerClaim.Contains("believe") || lowerClaim.Contains("trust"))
            return "Faith-centered";
        if (lowerClaim.Contains("love") || lowerClaim.Contains("compassion") || lowerClaim.Contains("mercy"))
            return "Emphasizes love";
        if (lowerClaim.Contains("action") || lowerClaim.Contains("do") || lowerClaim.Contains("work"))
            return "Action-oriented";
        if (lowerClaim.Contains("truth") || lowerClaim.Contains("scripture") || lowerClaim.Contains("word"))
            return "Scripture-focused";
        if (lowerClaim.Contains("pray") || lowerClaim.Contains("spirit"))
            return "Spiritually-minded";
        if (lowerClaim.Contains("suffer") || lowerClaim.Contains("trial") || lowerClaim.Contains("persever"))
            return "Acknowledges suffering";

        return null;
    }

    private void UpdateTopicStances(CharacterIntelligence intelligence, CharacterMemory memory)
    {
        var topic = ExtractMainTopic(memory.Context);
        if (string.IsNullOrEmpty(topic)) return;

        if (intelligence.TopicStances.TryGetValue(topic, out var existing))
        {
            existing.TimesDiscussed++;
            existing.SupportingArguments.AddRange(memory.ExtractedClaims.Take(2));
            existing.ScriptureReferences.AddRange(memory.ScripturesUsed);
            existing.ScriptureReferences = existing.ScriptureReferences.Distinct().ToList();
        }
        else
        {
            intelligence.TopicStances[topic] = new TopicStance
            {
                Topic = topic,
                Position = memory.ExtractedClaims.FirstOrDefault() ?? "",
                SupportingArguments = memory.ExtractedClaims.ToList(),
                ScriptureReferences = memory.ScripturesUsed.ToList(),
                Certainty = 0.5
            };
        }
    }

    private void UpdateRelationships(CharacterIntelligence intelligence, CharacterMemory memory)
    {
        foreach (var participant in memory.OtherParticipants)
        {
            if (intelligence.Relationships.TryGetValue(participant, out var relationship))
            {
                relationship.InteractionCount++;
                var topic = ExtractMainTopic(memory.Context);
                if (!string.IsNullOrEmpty(topic) && !relationship.SharedTopics.Contains(topic))
                {
                    relationship.SharedTopics.Add(topic);
                }
            }
            else
            {
                intelligence.Relationships[participant] = new CharacterRelationship
                {
                    OtherCharacterId = participant,
                    OtherCharacterName = participant,
                    Type = RelationshipType.Neutral,
                    InteractionCount = 1,
                    SharedTopics = new List<string> { ExtractMainTopic(memory.Context) }
                };
            }
        }
    }

    private List<TopicStance> FindRelevantStances(CharacterIntelligence intelligence, string context)
    {
        var contextWords = context.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        return intelligence.TopicStances.Values
            .Where(s => contextWords.Any(w => s.Topic.Contains(w) || w.Contains(s.Topic)))
            .OrderByDescending(s => s.TimesDiscussed)
            .ToList();
    }

    private async Task<List<CharacterMemory>> FindRelevantMemoriesAsync(
        CharacterIntelligence intelligence, 
        string context, 
        int maxCount)
    {
        // Simple keyword-based relevance for now
        // In production, this would use embeddings for semantic search
        var contextWords = context.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();

        var scored = intelligence.Memories
            .Select(m => new
            {
                Memory = m,
                Score = CalculateRelevanceScore(m, contextWords)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Memory.Importance)
            .Take(maxCount)
            .Select(x => x.Memory)
            .ToList();

        return await Task.FromResult(scored);
    }

    private double CalculateRelevanceScore(CharacterMemory memory, HashSet<string> contextWords)
    {
        var memoryText = $"{memory.Context} {memory.Response}".ToLower();
        var memoryWords = memoryText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var matchCount = memoryWords.Count(w => contextWords.Any(cw => w.Contains(cw) || cw.Contains(w)));
        
        // Boost recent memories slightly
        var recencyBoost = 1.0;
        var daysSince = (DateTime.UtcNow - memory.Timestamp).TotalDays;
        if (daysSince < 1) recencyBoost = 1.3;
        else if (daysSince < 7) recencyBoost = 1.1;

        return matchCount * recencyBoost * memory.Importance;
    }

    private string BuildProfileAnalysisPrompt(CharacterIntelligence intelligence)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Analyze the following responses from this character and write a brief (2-3 sentences) description of their evolved personality, communication style, and key themes.");
        sb.AppendLine();
        sb.AppendLine("Sample responses:");
        
        // Include diverse sample of memories
        var samples = intelligence.Memories
            .OrderByDescending(m => m.Importance)
            .Take(10)
            .ToList();

        foreach (var sample in samples)
        {
            sb.AppendLine($"- On \"{TruncateString(sample.Context, 40)}\": \"{TruncateString(sample.Response, 150)}\"");
        }

        sb.AppendLine();
        sb.AppendLine("Write the evolved personality description:");

        return sb.ToString();
    }

    private void AnalyzeCommunicationPatterns(CharacterIntelligence intelligence)
    {
        if (!intelligence.Memories.Any()) return;

        var style = intelligence.Profile.CommunicationStyle;
        var responses = intelligence.Memories.Select(m => m.Response).ToList();

        // Analyze formality
        var formalWords = new[] { "therefore", "thus", "indeed", "furthermore", "moreover", "verily" };
        var casualWords = new[] { "yeah", "well", "so", "like", "you know" };
        style.Formality = CalculateWordRatio(responses, formalWords, casualWords);

        // Analyze verbosity
        var avgLength = responses.Average(r => r.Split(' ').Length);
        style.Verbosity = Math.Min(1.0, avgLength / 200.0);

        // Analyze question asking
        var questionCount = responses.Count(r => r.Contains('?'));
        style.QuestionAsking = (double)questionCount / responses.Count;

        // Analyze storytelling
        var storyWords = new[] { "once", "when I", "there was", "I remember", "in those days" };
        style.StorytellingTendency = responses.Count(r => 
            storyWords.Any(sw => r.ToLower().Contains(sw))) / (double)responses.Count;
    }

    private double CalculateWordRatio(List<string> texts, string[] positiveWords, string[] negativeWords)
    {
        var allText = string.Join(" ", texts).ToLower();
        var words = allText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var posCount = words.Count(w => positiveWords.Any(p => w.Contains(p)));
        var negCount = words.Count(w => negativeWords.Any(n => w.Contains(n)));
        
        var total = posCount + negCount;
        if (total == 0) return 0.5;
        
        return (double)posCount / total;
    }

    private void ExtractSignaturePhrases(CharacterIntelligence intelligence)
    {
        // Find repeated phrases across responses
        var phrases = new Dictionary<string, int>();
        
        foreach (var memory in intelligence.Memories)
        {
            // Extract 3-5 word phrases
            var words = memory.Response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length - 3; i++)
            {
                var phrase = string.Join(" ", words.Skip(i).Take(4));
                phrase = phrase.Trim('.', ',', '!', '?', '"');
                
                if (phrase.Length > 10 && phrase.Length < 50)
                {
                    if (phrases.ContainsKey(phrase))
                        phrases[phrase]++;
                    else
                        phrases[phrase] = 1;
                }
            }
        }

        // Keep phrases that appear multiple times
        intelligence.Profile.SignaturePhrases = phrases
            .Where(kv => kv.Value >= 2)
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => kv.Key)
            .ToList();
    }

    private string TruncateString(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength);
    }

    #region Persistence

    private async Task SaveIntelligenceAsync(CharacterIntelligence intelligence)
    {
        var filePath = GetIntelligenceFilePath(intelligence.CharacterId);
        
        try
        {
            var json = JsonSerializer.Serialize(intelligence, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save intelligence for {CharacterId}", intelligence.CharacterId);
        }
    }

    private async Task<CharacterIntelligence?> LoadIntelligenceAsync(string characterId)
    {
        var filePath = GetIntelligenceFilePath(characterId);
        
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<CharacterIntelligence>(json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load intelligence for {CharacterId}", characterId);
            return null;
        }
    }

    private string GetIntelligenceFilePath(string characterId)
    {
        // Sanitize the character ID for use as filename
        var safeId = string.Join("_", characterId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_intelligenceDirectory, $"{safeId}.intelligence.json");
    }

    #endregion

    #endregion
}
