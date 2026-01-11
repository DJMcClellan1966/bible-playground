using System.Text.Json;

namespace AI_Bible_App.Maui.Services.Core;

/// <summary>
/// Intelligent conversation flow predictor that anticipates user questions,
/// pre-fetches likely responses, and suggests follow-up topics.
/// </summary>
public interface IConversationFlowPredictor
{
    Task<List<PredictedQuestion>> PredictNextQuestionsAsync(ConversationState state);
    Task<List<string>> GetSuggestedTopicsAsync(string characterName, string currentTopic);
    Task RecordQuestionAsync(string characterName, string question, string? followUp = null);
    Task<List<ConversationTemplate>> GetConversationTemplatesAsync(string characterName);
    Task PrefetchPredictedResponsesAsync(ConversationState state, Func<string, string, Task<string>> fetchFunc);
    double GetPredictionConfidence(string characterName, string predictedQuestion);
}

public class ConversationState
{
    public string CharacterName { get; set; } = "";
    public string CurrentTopic { get; set; } = "";
    public List<string> RecentQuestions { get; set; } = new();
    public List<string> RecentTopics { get; set; } = new();
    public int TurnCount { get; set; }
    public bool IsDeepDive { get; set; }
    public string? UserIntent { get; set; }
}

public class PredictedQuestion
{
    public string Question { get; set; } = "";
    public double Confidence { get; set; }
    public string Category { get; set; } = "";
    public bool IsPrefetched { get; set; }
    public string? PrefetchedResponse { get; set; }
}

public class ConversationTemplate
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CharacterName { get; set; } = "";
    public List<TemplateStep> Steps { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class TemplateStep
{
    public int Order { get; set; }
    public string SuggestedQuestion { get; set; } = "";
    public string Purpose { get; set; } = "";
    public List<string> FollowUpOptions { get; set; } = new();
}

public class QuestionPattern
{
    public string Pattern { get; set; } = "";
    public List<string> CommonFollowUps { get; set; } = new();
    public int Frequency { get; set; }
    public Dictionary<string, int> CharacterFrequency { get; set; } = new();
}

public class ConversationFlowPredictor : IConversationFlowPredictor
{
    private readonly Dictionary<string, List<QuestionPattern>> _patternDatabase;
    private readonly Dictionary<string, List<ConversationTemplate>> _templates;
    private readonly Dictionary<string, Dictionary<string, List<string>>> _topicConnections;
    private readonly Dictionary<string, string> _prefetchCache = new();
    private readonly string _persistPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ConversationFlowPredictor()
    {
        _persistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App", "conversation_patterns.json");
        
        _patternDatabase = InitializePatternDatabase();
        _templates = InitializeTemplates();
        _topicConnections = InitializeTopicConnections();
        
        _ = LoadPatternsAsync();
    }

    public async Task<List<PredictedQuestion>> PredictNextQuestionsAsync(ConversationState state)
    {
        var predictions = new List<PredictedQuestion>();
        
        // Get patterns for current topic
        var topicPatterns = GetPatternsForTopic(state.CurrentTopic);
        
        // Get character-specific patterns
        var characterPatterns = GetPatternsForCharacter(state.CharacterName);
        
        // Analyze recent questions for follow-up patterns
        var followUpPatterns = AnalyzeFollowUpPatterns(state.RecentQuestions);
        
        // Generate predictions based on conversation depth
        if (state.TurnCount <= 2)
        {
            // Early conversation - suggest broad exploratory questions
            predictions.AddRange(GenerateExploratoryQuestions(state));
        }
        else if (state.IsDeepDive)
        {
            // Deep dive - suggest more specific, detailed questions
            predictions.AddRange(GenerateDeepDiveQuestions(state));
        }
        else
        {
            // Normal flow - blend of follow-ups and new directions
            predictions.AddRange(GenerateBalancedQuestions(state));
        }
        
        // Add predictions from pattern matching
        foreach (var pattern in topicPatterns.Concat(characterPatterns).Take(5))
        {
            foreach (var followUp in pattern.CommonFollowUps.Take(2))
            {
                if (!predictions.Any(p => p.Question.Equals(followUp, StringComparison.OrdinalIgnoreCase)))
                {
                    predictions.Add(new PredictedQuestion
                    {
                        Question = followUp,
                        Confidence = CalculateConfidence(pattern, state),
                        Category = "Pattern Match"
                    });
                }
            }
        }
        
        // Check for prefetched responses
        foreach (var prediction in predictions)
        {
            var cacheKey = $"{state.CharacterName}:{prediction.Question}";
            if (_prefetchCache.TryGetValue(cacheKey, out var cached))
            {
                prediction.IsPrefetched = true;
                prediction.PrefetchedResponse = cached;
            }
        }
        
        // Sort by confidence and return top predictions
        return predictions
            .OrderByDescending(p => p.Confidence)
            .Take(5)
            .ToList();
    }

    public Task<List<string>> GetSuggestedTopicsAsync(string characterName, string currentTopic)
    {
        var suggestions = new List<string>();
        var normalizedCharacter = characterName.ToLowerInvariant();
        var normalizedTopic = currentTopic.ToLowerInvariant();
        
        // Get connected topics
        if (_topicConnections.TryGetValue(normalizedCharacter, out var charTopics))
        {
            if (charTopics.TryGetValue(normalizedTopic, out var connected))
            {
                suggestions.AddRange(connected);
            }
        }
        
        // Add general topic connections
        if (_topicConnections.TryGetValue("general", out var generalTopics))
        {
            if (generalTopics.TryGetValue(normalizedTopic, out var connected))
            {
                suggestions.AddRange(connected.Where(t => !suggestions.Contains(t)));
            }
        }
        
        // Add character-specific topics
        var charSpecificTopics = GetCharacterSpecificTopics(normalizedCharacter);
        suggestions.AddRange(charSpecificTopics.Where(t => 
            !suggestions.Contains(t) && 
            !t.Equals(normalizedTopic, StringComparison.OrdinalIgnoreCase)));
        
        return Task.FromResult(suggestions.Distinct().Take(8).ToList());
    }

    public async Task RecordQuestionAsync(string characterName, string question, string? followUp = null)
    {
        await _lock.WaitAsync();
        try
        {
            var normalizedChar = characterName.ToLowerInvariant();
            
            if (!_patternDatabase.TryGetValue(normalizedChar, out var patterns))
            {
                patterns = new List<QuestionPattern>();
                _patternDatabase[normalizedChar] = patterns;
            }
            
            // Find or create pattern
            var pattern = patterns.FirstOrDefault(p => 
                CalculateSimilarity(p.Pattern, question) > 0.7);
            
            if (pattern == null)
            {
                pattern = new QuestionPattern
                {
                    Pattern = question,
                    CommonFollowUps = new List<string>(),
                    Frequency = 0,
                    CharacterFrequency = new Dictionary<string, int>()
                };
                patterns.Add(pattern);
            }
            
            pattern.Frequency++;
            pattern.CharacterFrequency.TryGetValue(normalizedChar, out var charFreq);
            pattern.CharacterFrequency[normalizedChar] = charFreq + 1;
            
            if (!string.IsNullOrEmpty(followUp) && 
                !pattern.CommonFollowUps.Contains(followUp, StringComparer.OrdinalIgnoreCase))
            {
                pattern.CommonFollowUps.Add(followUp);
                
                // Keep only top follow-ups
                if (pattern.CommonFollowUps.Count > 10)
                {
                    pattern.CommonFollowUps = pattern.CommonFollowUps.Take(10).ToList();
                }
            }
            
            await SavePatternsAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<List<ConversationTemplate>> GetConversationTemplatesAsync(string characterName)
    {
        var normalizedChar = characterName.ToLowerInvariant();
        var result = new List<ConversationTemplate>();
        
        // Get character-specific templates
        foreach (var (key, templates) in _templates)
        {
            if (normalizedChar.Contains(key) || key == "general")
            {
                result.AddRange(templates);
            }
        }
        
        return Task.FromResult(result.DistinctBy(t => t.Name).ToList());
    }

    public async Task PrefetchPredictedResponsesAsync(ConversationState state, 
        Func<string, string, Task<string>> fetchFunc)
    {
        var predictions = await PredictNextQuestionsAsync(state);
        var highConfidence = predictions.Where(p => p.Confidence > 0.6 && !p.IsPrefetched).Take(2);
        
        foreach (var prediction in highConfidence)
        {
            var cacheKey = $"{state.CharacterName}:{prediction.Question}";
            
            if (!_prefetchCache.ContainsKey(cacheKey))
            {
                try
                {
                    var response = await fetchFunc(state.CharacterName, prediction.Question);
                    _prefetchCache[cacheKey] = response;
                }
                catch
                {
                    // Prefetch failed silently - not critical
                }
            }
        }
    }

    public double GetPredictionConfidence(string characterName, string predictedQuestion)
    {
        var normalizedChar = characterName.ToLowerInvariant();
        
        if (_patternDatabase.TryGetValue(normalizedChar, out var patterns))
        {
            var matchingPattern = patterns.FirstOrDefault(p => 
                CalculateSimilarity(p.Pattern, predictedQuestion) > 0.7);
            
            if (matchingPattern != null)
            {
                // Base confidence on frequency
                var baseConfidence = Math.Min(0.9, matchingPattern.Frequency * 0.1);
                
                // Boost for character-specific patterns
                if (matchingPattern.CharacterFrequency.TryGetValue(normalizedChar, out var charFreq))
                {
                    baseConfidence += Math.Min(0.1, charFreq * 0.02);
                }
                
                return Math.Min(0.95, baseConfidence);
            }
        }
        
        return 0.3; // Default confidence for unknown patterns
    }

    private Dictionary<string, List<QuestionPattern>> InitializePatternDatabase()
    {
        return new Dictionary<string, List<QuestionPattern>>(StringComparer.OrdinalIgnoreCase)
        {
            ["peter"] = new List<QuestionPattern>
            {
                new() { Pattern = "faith", CommonFollowUps = new List<string> 
                    { "How can I strengthen my faith?", "What about when I doubt?", "How did you walk on water?" }, Frequency = 5 },
                new() { Pattern = "denial", CommonFollowUps = new List<string> 
                    { "How did Jesus forgive you?", "What did you learn?", "How can I overcome my failures?" }, Frequency = 4 },
                new() { Pattern = "church leadership", CommonFollowUps = new List<string> 
                    { "How do you lead with humility?", "What makes a good shepherd?", "How did the early church grow?" }, Frequency = 3 }
            },
            ["paul"] = new List<QuestionPattern>
            {
                new() { Pattern = "grace", CommonFollowUps = new List<string> 
                    { "What about works?", "How is grace different from license?", "How did grace change you?" }, Frequency = 6 },
                new() { Pattern = "suffering", CommonFollowUps = new List<string> 
                    { "How do you endure?", "What's the purpose of trials?", "Tell me about your thorn" }, Frequency = 5 },
                new() { Pattern = "Damascus", CommonFollowUps = new List<string> 
                    { "What did you see?", "How did it feel to be blind?", "What happened next?" }, Frequency = 4 }
            },
            ["john"] = new List<QuestionPattern>
            {
                new() { Pattern = "love", CommonFollowUps = new List<string> 
                    { "How do we love enemies?", "What is perfect love?", "Why is love so important?" }, Frequency = 7 },
                new() { Pattern = "truth", CommonFollowUps = new List<string> 
                    { "How do we know truth?", "What about relativism?", "How is Jesus the truth?" }, Frequency = 4 },
                new() { Pattern = "eternal life", CommonFollowUps = new List<string> 
                    { "How do we receive it?", "What will heaven be like?", "Can we lose salvation?" }, Frequency = 5 }
            }
        };
    }

    private Dictionary<string, List<ConversationTemplate>> InitializeTemplates()
    {
        return new Dictionary<string, List<ConversationTemplate>>(StringComparer.OrdinalIgnoreCase)
        {
            ["general"] = new List<ConversationTemplate>
            {
                new ConversationTemplate
                {
                    Name = "Life Story",
                    Description = "Explore a character's life journey and key moments",
                    Steps = new List<TemplateStep>
                    {
                        new() { Order = 1, SuggestedQuestion = "What was your life like before your calling?", 
                            Purpose = "Background", FollowUpOptions = new List<string> { "What changed?", "How did you feel?" } },
                        new() { Order = 2, SuggestedQuestion = "What was your greatest challenge?",
                            Purpose = "Struggle", FollowUpOptions = new List<string> { "How did you overcome it?", "What did you learn?" } },
                        new() { Order = 3, SuggestedQuestion = "What would you tell your younger self?",
                            Purpose = "Wisdom", FollowUpOptions = new List<string> { "Any regrets?", "What are you most proud of?" } }
                    },
                    Tags = new List<string> { "biography", "journey", "personal" }
                },
                new ConversationTemplate
                {
                    Name = "Spiritual Guidance",
                    Description = "Seek wisdom on faith matters",
                    Steps = new List<TemplateStep>
                    {
                        new() { Order = 1, SuggestedQuestion = "How do you maintain your faith during hard times?",
                            Purpose = "Perseverance", FollowUpOptions = new List<string> { "What helps most?", "Any specific practices?" } },
                        new() { Order = 2, SuggestedQuestion = "What scripture comforts you most?",
                            Purpose = "Scripture", FollowUpOptions = new List<string> { "Why that passage?", "How do you apply it?" } },
                        new() { Order = 3, SuggestedQuestion = "What advice would you give someone struggling?",
                            Purpose = "Application", FollowUpOptions = new List<string> { "Where do I start?", "How do I know if I'm on the right path?" } }
                    },
                    Tags = new List<string> { "guidance", "faith", "wisdom" }
                }
            },
            ["peter"] = new List<ConversationTemplate>
            {
                new ConversationTemplate
                {
                    Name = "Walking with Jesus",
                    Description = "Learn about Peter's time with Jesus",
                    Steps = new List<TemplateStep>
                    {
                        new() { Order = 1, SuggestedQuestion = "What was it like to first meet Jesus?",
                            Purpose = "Beginning", FollowUpOptions = new List<string> { "What drew you to him?", "Did you know who he was?" } },
                        new() { Order = 2, SuggestedQuestion = "Tell me about walking on water",
                            Purpose = "Faith moment", FollowUpOptions = new List<string> { "Why did you sink?", "What did you learn?" } },
                        new() { Order = 3, SuggestedQuestion = "What was the Transfiguration like?",
                            Purpose = "Revelation", FollowUpOptions = new List<string> { "How did it change you?", "Why build tents?" } }
                    },
                    Tags = new List<string> { "disciple", "witness", "jesus" }
                }
            }
        };
    }

    private Dictionary<string, Dictionary<string, List<string>>> InitializeTopicConnections()
    {
        return new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["general"] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["faith"] = new List<string> { "doubt", "trust", "belief", "hope", "prayer" },
                ["love"] = new List<string> { "forgiveness", "grace", "compassion", "mercy", "sacrifice" },
                ["suffering"] = new List<string> { "hope", "perseverance", "comfort", "purpose", "growth" },
                ["prayer"] = new List<string> { "faith", "worship", "guidance", "intercession", "communion" },
                ["salvation"] = new List<string> { "grace", "faith", "redemption", "eternal life", "forgiveness" },
                ["forgiveness"] = new List<string> { "grace", "love", "reconciliation", "mercy", "healing" }
            },
            ["peter"] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["denial"] = new List<string> { "forgiveness", "restoration", "failure", "redemption" },
                ["rock"] = new List<string> { "church", "leadership", "foundation", "keys" },
                ["fishing"] = new List<string> { "calling", "discipleship", "miracles", "breakfast on beach" }
            },
            ["paul"] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["damascus"] = new List<string> { "conversion", "calling", "blindness", "transformation" },
                ["thorn"] = new List<string> { "weakness", "grace", "suffering", "strength" },
                ["gentiles"] = new List<string> { "mission", "inclusion", "gospel", "churches" }
            }
        };
    }

    private List<QuestionPattern> GetPatternsForTopic(string topic)
    {
        var results = new List<QuestionPattern>();
        var keywords = topic.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var patterns in _patternDatabase.Values)
        {
            results.AddRange(patterns.Where(p => 
                keywords.Any(k => p.Pattern.Contains(k, StringComparison.OrdinalIgnoreCase))));
        }
        
        return results.OrderByDescending(p => p.Frequency).ToList();
    }

    private List<QuestionPattern> GetPatternsForCharacter(string characterName)
    {
        var normalized = characterName.ToLowerInvariant();
        
        foreach (var (key, patterns) in _patternDatabase)
        {
            if (normalized.Contains(key))
            {
                return patterns.OrderByDescending(p => p.Frequency).ToList();
            }
        }
        
        return new List<QuestionPattern>();
    }

    private List<QuestionPattern> AnalyzeFollowUpPatterns(List<string> recentQuestions)
    {
        var patterns = new List<QuestionPattern>();
        
        if (recentQuestions.Count < 2) return patterns;
        
        var lastQuestion = recentQuestions.LastOrDefault()?.ToLowerInvariant() ?? "";
        
        foreach (var patternList in _patternDatabase.Values)
        {
            foreach (var pattern in patternList)
            {
                if (CalculateSimilarity(pattern.Pattern, lastQuestion) > 0.5)
                {
                    patterns.Add(pattern);
                }
            }
        }
        
        return patterns;
    }

    private List<PredictedQuestion> GenerateExploratoryQuestions(ConversationState state)
    {
        var questions = new List<PredictedQuestion>();
        var charLower = state.CharacterName.ToLowerInvariant();
        
        // Add character-specific exploratory questions
        if (charLower.Contains("peter"))
        {
            questions.Add(new PredictedQuestion { Question = "What was your first impression of Jesus?", Confidence = 0.7, Category = "Exploratory" });
            questions.Add(new PredictedQuestion { Question = "What's the most important lesson you learned?", Confidence = 0.65, Category = "Exploratory" });
        }
        else if (charLower.Contains("paul"))
        {
            questions.Add(new PredictedQuestion { Question = "How did your life change after Damascus?", Confidence = 0.7, Category = "Exploratory" });
            questions.Add(new PredictedQuestion { Question = "What drives your missionary work?", Confidence = 0.65, Category = "Exploratory" });
        }
        else if (charLower.Contains("john"))
        {
            questions.Add(new PredictedQuestion { Question = "Why do you write so much about love?", Confidence = 0.7, Category = "Exploratory" });
            questions.Add(new PredictedQuestion { Question = "What was Jesus like as a friend?", Confidence = 0.65, Category = "Exploratory" });
        }
        
        // General exploratory questions
        questions.Add(new PredictedQuestion { Question = "What would you tell someone just starting their faith journey?", Confidence = 0.6, Category = "Exploratory" });
        
        return questions;
    }

    private List<PredictedQuestion> GenerateDeepDiveQuestions(ConversationState state)
    {
        var questions = new List<PredictedQuestion>();
        var topic = state.CurrentTopic.ToLowerInvariant();
        
        // Topic-specific deep dive questions
        if (topic.Contains("faith"))
        {
            questions.Add(new PredictedQuestion { Question = "Can you give me a specific example from your experience?", Confidence = 0.75, Category = "Deep Dive" });
            questions.Add(new PredictedQuestion { Question = "What Scripture passage best captures this truth?", Confidence = 0.7, Category = "Deep Dive" });
        }
        else if (topic.Contains("suffering") || topic.Contains("trial"))
        {
            questions.Add(new PredictedQuestion { Question = "How did you find hope in your darkest moment?", Confidence = 0.75, Category = "Deep Dive" });
            questions.Add(new PredictedQuestion { Question = "What would you say to someone going through this now?", Confidence = 0.7, Category = "Deep Dive" });
        }
        
        return questions;
    }

    private List<PredictedQuestion> GenerateBalancedQuestions(ConversationState state)
    {
        var questions = new List<PredictedQuestion>();
        
        // Mix of follow-up and new direction
        questions.Add(new PredictedQuestion { Question = "Can you tell me more about that?", Confidence = 0.5, Category = "Follow-up" });
        questions.Add(new PredictedQuestion { Question = "How does that relate to modern life?", Confidence = 0.55, Category = "Application" });
        
        // Get suggested topics for new directions
        var suggestedTopics = GetCharacterSpecificTopics(state.CharacterName.ToLowerInvariant());
        foreach (var topic in suggestedTopics.Take(2))
        {
            if (!state.RecentTopics.Contains(topic))
            {
                questions.Add(new PredictedQuestion 
                { 
                    Question = $"What are your thoughts on {topic}?", 
                    Confidence = 0.5, 
                    Category = "New Direction" 
                });
            }
        }
        
        return questions;
    }

    private List<string> GetCharacterSpecificTopics(string characterName)
    {
        return characterName switch
        {
            var c when c.Contains("peter") => new List<string> { "fishing", "the rock", "keys of the kingdom", "leadership", "denial and restoration" },
            var c when c.Contains("paul") => new List<string> { "grace vs law", "spiritual gifts", "suffering for Christ", "church planting", "the body of Christ" },
            var c when c.Contains("john") => new List<string> { "love", "light vs darkness", "truth", "eternal life", "abiding in Christ" },
            var c when c.Contains("moses") => new List<string> { "the law", "exodus", "the wilderness", "meeting God", "leadership" },
            var c when c.Contains("david") => new List<string> { "worship", "psalms", "kingship", "repentance", "heart for God" },
            _ => new List<string> { "faith", "hope", "love", "prayer", "purpose" }
        };
    }

    private double CalculateConfidence(QuestionPattern pattern, ConversationState state)
    {
        var baseConfidence = Math.Min(0.7, pattern.Frequency * 0.1);
        
        // Boost for topic match
        if (state.CurrentTopic.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase))
        {
            baseConfidence += 0.15;
        }
        
        // Boost for character-specific frequency
        var charLower = state.CharacterName.ToLowerInvariant();
        foreach (var (key, freq) in pattern.CharacterFrequency)
        {
            if (charLower.Contains(key))
            {
                baseConfidence += Math.Min(0.1, freq * 0.02);
                break;
            }
        }
        
        return Math.Min(0.9, baseConfidence);
    }

    private double CalculateSimilarity(string s1, string s2)
    {
        var words1 = s1.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = s2.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        
        if (words1.Count == 0 || words2.Count == 0) return 0;
        
        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();
        
        return (double)intersection / union;
    }

    private async Task SavePatternsAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_persistPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var json = JsonSerializer.Serialize(_patternDatabase);
            await File.WriteAllTextAsync(_persistPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pattern save error: {ex.Message}");
        }
    }

    private async Task LoadPatternsAsync()
    {
        try
        {
            if (File.Exists(_persistPath))
            {
                var json = await File.ReadAllTextAsync(_persistPath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, List<QuestionPattern>>>(json);
                
                if (loaded != null)
                {
                    foreach (var (key, patterns) in loaded)
                    {
                        if (_patternDatabase.ContainsKey(key))
                        {
                            // Merge patterns
                            foreach (var pattern in patterns)
                            {
                                var existing = _patternDatabase[key]
                                    .FirstOrDefault(p => CalculateSimilarity(p.Pattern, pattern.Pattern) > 0.8);
                                
                                if (existing != null)
                                {
                                    existing.Frequency += pattern.Frequency;
                                    existing.CommonFollowUps = existing.CommonFollowUps
                                        .Union(pattern.CommonFollowUps)
                                        .Take(10)
                                        .ToList();
                                }
                                else
                                {
                                    _patternDatabase[key].Add(pattern);
                                }
                            }
                        }
                        else
                        {
                            _patternDatabase[key] = patterns;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pattern load error: {ex.Message}");
        }
    }
}
