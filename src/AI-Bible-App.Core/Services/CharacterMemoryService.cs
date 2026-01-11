using System.Text.Json;
using System.Text.RegularExpressions;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Core.Services;

/// <summary>
/// Manages persistent memory of what biblical characters have learned about users.
/// Uses JSON file storage per user for simplicity and data portability.
/// </summary>
public class CharacterMemoryService : ICharacterMemoryService
{
    private readonly ILogger<CharacterMemoryService> _logger;
    private readonly string _memoryStoragePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    
    // Emotion detection patterns
    private static readonly Dictionary<string, string[]> EmotionPatterns = new()
    {
        ["sadness"] = new[] { "sad", "depressed", "crying", "tears", "grief", "loss", "mourning", "hurt", "broken", "devastated" },
        ["anxiety"] = new[] { "anxious", "worried", "scared", "fear", "panic", "nervous", "stress", "overwhelmed", "dread" },
        ["anger"] = new[] { "angry", "furious", "mad", "rage", "frustrated", "annoyed", "bitter", "resentful", "hate" },
        ["loneliness"] = new[] { "lonely", "alone", "isolated", "abandoned", "rejected", "nobody", "friendless" },
        ["confusion"] = new[] { "confused", "lost", "uncertain", "don't know", "unclear", "questioning", "doubt" },
        ["hope"] = new[] { "hopeful", "better", "improving", "looking forward", "optimistic", "encouraged" },
        ["gratitude"] = new[] { "thankful", "grateful", "blessed", "appreciate", "thank you", "thanks" },
        ["guilt"] = new[] { "guilty", "ashamed", "regret", "sorry", "fault", "blame myself", "should have" },
        ["joy"] = new[] { "happy", "joyful", "excited", "wonderful", "great", "amazing", "blessed" }
    };
    
    // Topic detection patterns
    private static readonly Dictionary<string, string[]> TopicPatterns = new()
    {
        ["family"] = new[] { "family", "parent", "mother", "father", "child", "son", "daughter", "sibling", "brother", "sister", "spouse", "husband", "wife", "marriage" },
        ["work"] = new[] { "job", "work", "career", "boss", "coworker", "colleague", "office", "employment", "fired", "hired", "promotion" },
        ["health"] = new[] { "sick", "illness", "disease", "doctor", "hospital", "diagnosis", "health", "pain", "surgery", "treatment" },
        ["faith"] = new[] { "faith", "believe", "God", "pray", "church", "scripture", "Bible", "doubt", "spiritual", "worship", "sin" },
        ["relationships"] = new[] { "friend", "relationship", "dating", "breakup", "divorce", "trust", "betrayal", "forgiveness" },
        ["purpose"] = new[] { "purpose", "meaning", "calling", "direction", "future", "plan", "destiny", "why am I" },
        ["finances"] = new[] { "money", "debt", "bills", "financial", "broke", "savings", "expense", "afford" },
        ["grief"] = new[] { "died", "death", "passed away", "funeral", "grieving", "loss", "miss them", "gone" },
        ["decisions"] = new[] { "decide", "choice", "should I", "decision", "option", "path", "which way", "crossroads" }
    };

    public CharacterMemoryService(ILogger<CharacterMemoryService> logger)
    {
        _logger = logger;
        // Store in app data folder
        _memoryStoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AI-Bible-App",
            "memories");
        
        Directory.CreateDirectory(_memoryStoragePath);
    }
    
    public async Task<UserCharacterMemory?> GetMemoryAsync(string userId, string characterId)
    {
        var filePath = GetMemoryFilePath(userId);
        
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
                return null;
                
            var json = await File.ReadAllTextAsync(filePath);
            var memories = JsonSerializer.Deserialize<List<UserCharacterMemory>>(json);
            
            return memories?.FirstOrDefault(m => 
                m.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read memory for user {UserId}, character {CharacterId}", userId, characterId);
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }
    
    public async Task<List<UserCharacterMemory>> GetAllMemoriesForUserAsync(string userId)
    {
        var filePath = GetMemoryFilePath(userId);
        
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
                return new List<UserCharacterMemory>();
                
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<UserCharacterMemory>>(json) ?? new List<UserCharacterMemory>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read all memories for user {UserId}", userId);
            return new List<UserCharacterMemory>();
        }
        finally
        {
            _fileLock.Release();
        }
    }
    
    public async Task SaveMemoryAsync(UserCharacterMemory memory)
    {
        memory.LastInteraction = DateTime.UtcNow;
        
        var filePath = GetMemoryFilePath(memory.UserId);
        
        await _fileLock.WaitAsync();
        try
        {
            var memories = new List<UserCharacterMemory>();
            
            if (File.Exists(filePath))
            {
                var existingJson = await File.ReadAllTextAsync(filePath);
                memories = JsonSerializer.Deserialize<List<UserCharacterMemory>>(existingJson) ?? new List<UserCharacterMemory>();
            }
            
            // Remove existing memory for this character if present
            memories.RemoveAll(m => 
                m.CharacterId.Equals(memory.CharacterId, StringComparison.OrdinalIgnoreCase) &&
                m.UserId.Equals(memory.UserId, StringComparison.OrdinalIgnoreCase));
            
            // Add updated memory
            memories.Add(memory);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(memories, options);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Saved memory for user {UserId}, character {CharacterId}", memory.UserId, memory.CharacterId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save memory for user {UserId}, character {CharacterId}", memory.UserId, memory.CharacterId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }
    
    public async Task RecordInteractionAsync(string userId, string characterId, string userMessage, string characterResponse)
    {
        var memory = await GetMemoryAsync(userId, characterId) ?? new UserCharacterMemory
        {
            UserId = userId,
            CharacterId = characterId
        };
        
        memory.ConversationCount++;
        memory.LastInteraction = DateTime.UtcNow;
        
        // Detect emotions in user message
        var detectedEmotions = DetectEmotions(userMessage);
        foreach (var (emotion, triggers) in detectedEmotions)
        {
            var existing = memory.EmotionalPatterns.FirstOrDefault(e => 
                e.Emotion.Equals(emotion, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                existing.Frequency++;
                existing.LastObserved = DateTime.UtcNow;
                // Add any new triggers
                foreach (var trigger in triggers.Where(t => !existing.Triggers.Contains(t)))
                {
                    existing.Triggers.Add(trigger);
                }
            }
            else
            {
                memory.EmotionalPatterns.Add(new EmotionalInsight
                {
                    Emotion = emotion,
                    Frequency = 1,
                    Triggers = triggers,
                    LastObserved = DateTime.UtcNow
                });
            }
        }
        
        // Detect topics
        var detectedTopics = DetectTopics(userMessage);
        foreach (var topic in detectedTopics)
        {
            var existing = memory.RecurringTopics.FirstOrDefault(t => 
                t.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                existing.MentionCount++;
                existing.LastMentioned = DateTime.UtcNow;
            }
            else
            {
                memory.RecurringTopics.Add(new RecurringTopic
                {
                    Topic = topic,
                    MentionCount = 1,
                    LastMentioned = DateTime.UtcNow
                });
            }
        }
        
        // Detect life situations
        var situations = DetectLifeSituations(userMessage);
        foreach (var situation in situations)
        {
            var existing = memory.LifeSituations.FirstOrDefault(s => 
                s.Summary.Contains(situation, StringComparison.OrdinalIgnoreCase) ||
                situation.Contains(s.Summary, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                existing.MentionCount++;
                existing.LastMentioned = DateTime.UtcNow;
            }
            else
            {
                memory.LifeSituations.Add(new UserLifeSituation
                {
                    Summary = situation,
                    Category = DetermineCategory(situation),
                    FirstMentioned = DateTime.UtcNow,
                    LastMentioned = DateTime.UtcNow,
                    MentionCount = 1
                });
            }
        }
        
        await SaveMemoryAsync(memory);
    }
    
    public async Task<ConversationInsights> ExtractAndStoreInsightsAsync(
        string userId, 
        string characterId, 
        List<ChatMessage> conversation)
    {
        var insights = new ConversationInsights();
        var memory = await GetMemoryAsync(userId, characterId) ?? new UserCharacterMemory
        {
            UserId = userId,
            CharacterId = characterId
        };
        
        // Analyze all user messages in the conversation
        var userMessages = conversation.Where(m => m.Role == "user").Select(m => m.Content);
        var fullUserText = string.Join(" ", userMessages);
        
        // Extract emotions
        var emotions = DetectEmotions(fullUserText);
        insights.DetectedEmotions = emotions.Select(e => e.emotion).ToList();
        
        // Extract topics
        insights.DiscussedTopics = DetectTopics(fullUserText);
        
        // Look for life situations mentioned
        insights.MentionedSituations = DetectLifeSituations(fullUserText);
        foreach (var situation in insights.MentionedSituations)
        {
            if (!memory.LifeSituations.Any(s => s.Summary.Contains(situation, StringComparison.OrdinalIgnoreCase)))
            {
                memory.LifeSituations.Add(new UserLifeSituation
                {
                    Summary = situation,
                    Category = DetermineCategory(situation),
                    FirstMentioned = DateTime.UtcNow,
                    LastMentioned = DateTime.UtcNow,
                    MentionCount = 1
                });
            }
        }
        
        // Detect scripture references in responses
        insights.ReferencedScriptures = DetectScriptureReferences(
            string.Join(" ", conversation.Where(m => m.Role == "assistant").Select(m => m.Content)));
        
        // Check for breakthrough indicators
        var lastUserMessage = conversation.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        if (ContainsBreakthroughIndicators(lastUserMessage))
        {
            insights.WasBreakthroughMoment = true;
            insights.BreakthroughSummary = ExtractBreakthroughSummary(lastUserMessage);
            
            memory.SignificantMoments.Add(new SignificantMoment
            {
                Timestamp = DateTime.UtcNow,
                Summary = insights.BreakthroughSummary ?? "A meaningful moment of understanding",
                UserMessage = lastUserMessage.Length > 200 ? lastUserMessage.Substring(0, 200) + "..." : lastUserMessage,
                WhySignificant = "breakthrough"
            });
        }
        
        // Infer communication preferences from message patterns
        insights.InferredPreferences = InferCommunicationPreferences(userMessages.ToList());
        if (insights.InferredPreferences != null)
        {
            // Blend with existing preferences
            memory.CommunicationStyle.PrefersDirectness ??= insights.InferredPreferences.PrefersDirectness;
            memory.CommunicationStyle.WantsScriptureReferences ??= insights.InferredPreferences.WantsScriptureReferences;
            memory.CommunicationStyle.PrefersQuestions ??= insights.InferredPreferences.PrefersQuestions;
            memory.CommunicationStyle.NeedsEncouragement ??= insights.InferredPreferences.NeedsEncouragement;
        }
        
        await SaveMemoryAsync(memory);
        
        return insights;
    }
    
    public async Task<string> GetContextForPromptAsync(string userId, string characterId)
    {
        var memory = await GetMemoryAsync(userId, characterId);
        if (memory == null)
            return "This is your first conversation with this person. Be warm and welcoming.";
        
        return memory.GenerateContextSummary();
    }
    
    public async Task MarkSituationResolvedAsync(string userId, string characterId, string situationId, string? resolution)
    {
        var memory = await GetMemoryAsync(userId, characterId);
        if (memory == null) return;
        
        var situation = memory.LifeSituations.FirstOrDefault(s => s.Id == situationId);
        if (situation != null)
        {
            situation.IsResolved = true;
            situation.Resolution = resolution;
        }
        
        await SaveMemoryAsync(memory);
    }
    
    public async Task RecordResonantScriptureAsync(string userId, string characterId, string reference, string? reason)
    {
        var memory = await GetMemoryAsync(userId, characterId);
        if (memory == null)
        {
            memory = new UserCharacterMemory
            {
                UserId = userId,
                CharacterId = characterId
            };
        }
        
        var existing = memory.ResonantScriptures.FirstOrDefault(s => 
            s.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
        {
            existing.TimesReferenced++;
        }
        else
        {
            memory.ResonantScriptures.Add(new ResonantScripture
            {
                Reference = reference,
                WhyItResonated = reason,
                FirstShared = DateTime.UtcNow,
                TimesReferenced = 1
            });
        }
        
        await SaveMemoryAsync(memory);
    }
    
    public async Task ClearUserMemoryAsync(string userId)
    {
        var filePath = GetMemoryFilePath(userId);
        
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Cleared all memory for user {UserId}", userId);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }
    
    public async Task<string> ExportUserMemoryAsync(string userId)
    {
        var memories = await GetAllMemoriesForUserAsync(userId);
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(memories, options);
    }
    
    #region Private Helper Methods
    
    private string GetMemoryFilePath(string userId)
    {
        // Sanitize user ID for filename
        var sanitized = Regex.Replace(userId, @"[^a-zA-Z0-9_-]", "_");
        return Path.Combine(_memoryStoragePath, $"{sanitized}_memories.json");
    }
    
    private List<(string emotion, List<string> triggers)> DetectEmotions(string text)
    {
        var results = new List<(string emotion, List<string> triggers)>();
        var lowerText = text.ToLower();
        
        foreach (var (emotion, patterns) in EmotionPatterns)
        {
            var matchedPatterns = patterns.Where(p => lowerText.Contains(p)).ToList();
            if (matchedPatterns.Any())
            {
                results.Add((emotion, matchedPatterns));
            }
        }
        
        return results;
    }
    
    private List<string> DetectTopics(string text)
    {
        var results = new List<string>();
        var lowerText = text.ToLower();
        
        foreach (var (topic, patterns) in TopicPatterns)
        {
            if (patterns.Any(p => lowerText.Contains(p)))
            {
                results.Add(topic);
            }
        }
        
        return results;
    }
    
    private List<string> DetectLifeSituations(string text)
    {
        var situations = new List<string>();
        var lowerText = text.ToLower();
        
        // Look for situation indicators
        var situationPatterns = new[]
        {
            @"I(?:'m| am) (?:going through|dealing with|struggling with|facing) (.+?)(?:\.|,|$)",
            @"(?:my|our) (.+?) (?:is|are) (?:sick|dying|leaving|struggling)",
            @"I(?:'ve| have) been (?:having|experiencing) (.+?)(?:\.|,|$)",
            @"I (?:just|recently) (?:lost|found out|discovered) (.+?)(?:\.|,|$)"
        };
        
        foreach (var pattern in situationPatterns)
        {
            var matches = Regex.Matches(lowerText, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var captured = match.Groups[1].Value.Trim();
                    if (captured.Length > 5 && captured.Length < 100)
                    {
                        situations.Add(captured);
                    }
                }
            }
        }
        
        return situations;
    }
    
    private string DetermineCategory(string situation)
    {
        var lowerSituation = situation.ToLower();
        
        if (TopicPatterns["family"].Any(p => lowerSituation.Contains(p))) return "family";
        if (TopicPatterns["work"].Any(p => lowerSituation.Contains(p))) return "work";
        if (TopicPatterns["health"].Any(p => lowerSituation.Contains(p))) return "health";
        if (TopicPatterns["faith"].Any(p => lowerSituation.Contains(p))) return "faith";
        if (TopicPatterns["relationships"].Any(p => lowerSituation.Contains(p))) return "relationship";
        if (TopicPatterns["finances"].Any(p => lowerSituation.Contains(p))) return "finances";
        
        return "general";
    }
    
    private List<string> DetectScriptureReferences(string text)
    {
        var references = new List<string>();
        
        // Common Bible reference patterns
        var patterns = new[]
        {
            @"(\d?\s?[A-Za-z]+)\s+(\d+):(\d+(?:-\d+)?)",  // Book Chapter:Verse
            @"(Psalm|Proverbs|Romans|John|Matthew|Genesis|Exodus|Isaiah)\s+(\d+)",  // Just book and chapter
        };
        
        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(text, pattern);
            foreach (Match match in matches)
            {
                references.Add(match.Value.Trim());
            }
        }
        
        return references.Distinct().ToList();
    }
    
    private bool ContainsBreakthroughIndicators(string text)
    {
        var lowerText = text.ToLower();
        var indicators = new[]
        {
            "thank you",
            "that really helped",
            "i never thought of it that way",
            "now i understand",
            "you're right",
            "i feel better",
            "this makes sense",
            "i needed to hear that",
            "wow",
            "that's exactly what i needed"
        };
        
        return indicators.Any(i => lowerText.Contains(i));
    }
    
    private string ExtractBreakthroughSummary(string text)
    {
        // Try to capture what resonated
        if (text.Length <= 100)
            return text;
        
        // Take the most relevant portion
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var meaningfulSentence = sentences.FirstOrDefault(s => 
            s.ToLower().Contains("thank") || 
            s.ToLower().Contains("helped") || 
            s.ToLower().Contains("understand"));
        
        return meaningfulSentence?.Trim() ?? text.Substring(0, Math.Min(100, text.Length));
    }
    
    private CommunicationPreferences? InferCommunicationPreferences(List<string> messages)
    {
        if (!messages.Any()) return null;
        
        var prefs = new CommunicationPreferences();
        var allText = string.Join(" ", messages).ToLower();
        
        // Direct questions suggest preference for direct advice
        var questionCount = messages.Count(m => m.Contains("?"));
        var avgMessageLength = messages.Average(m => m.Length);
        
        // Many questions = wants direct answers
        prefs.PrefersDirectness = questionCount > messages.Count / 2;
        
        // Mentions of Bible/scripture = wants scripture focus
        prefs.WantsScriptureReferences = allText.Contains("bible") || allText.Contains("scripture") || 
                                         allText.Contains("verse");
        
        // Emotional content = needs encouragement
        var emotionalWords = new[] { "feel", "hurt", "sad", "happy", "angry", "worried", "scared" };
        prefs.NeedsEncouragement = emotionalWords.Any(w => allText.Contains(w));
        
        return prefs;
    }
    
    #endregion
}
