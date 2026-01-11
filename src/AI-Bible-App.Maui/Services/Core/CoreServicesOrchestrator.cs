namespace AI_Bible_App.Maui.Services.Core;

/// <summary>
/// Central orchestrator that coordinates all core services to provide
/// an optimized, intelligent character interaction experience.
/// </summary>
public interface ICoreServicesOrchestrator
{
    Task<EnhancedResponse> GetEnhancedResponseAsync(string characterName, string question);
    Task<ConversationEnhancement> GetConversationEnhancementAsync(string characterName, string topic);
    Task RecordInteractionAsync(string characterName, string question, string response);
    Task<SystemHealth> GetSystemHealthAsync();
    Task WarmupAsync();
}

public class EnhancedResponse
{
    public string Response { get; set; } = "";
    public bool WasCached { get; set; }
    public double CacheConfidence { get; set; }
    public string MoodModifier { get; set; } = "";
    public CharacterMood CurrentMood { get; set; } = new();
    public ScriptureContext? ScriptureContext { get; set; }
    public List<PredictedQuestion> SuggestedFollowUps { get; set; } = new();
    public PerformanceMetrics Metrics { get; set; } = new();
}

public class ConversationEnhancement
{
    public List<PredictedQuestion> PredictedQuestions { get; set; } = new();
    public List<string> SuggestedTopics { get; set; } = new();
    public ScriptureContext ScriptureContext { get; set; } = new();
    public CharacterMood CurrentMood { get; set; } = new();
    public string MoodPromptModifier { get; set; } = "";
    public List<ConversationTemplate> Templates { get; set; } = new();
}

public class PerformanceMetrics
{
    public TimeSpan TotalTime { get; set; }
    public TimeSpan CacheLookupTime { get; set; }
    public TimeSpan MoodProcessingTime { get; set; }
    public TimeSpan ScriptureTime { get; set; }
    public TimeSpan PredictionTime { get; set; }
    public bool UsedCache { get; set; }
}

public class SystemHealth
{
    public bool IsHealthy { get; set; }
    public CacheStatistics CacheStats { get; set; } = new();
    public PerformanceReport PerformanceReport { get; set; } = new();
    public HealthCheck HealthCheck { get; set; } = new();
    public List<OptimizationSuggestion> Suggestions { get; set; } = new();
}

public class CoreServicesOrchestrator : ICoreServicesOrchestrator
{
    private readonly IIntelligentCacheService _cache;
    private readonly ICharacterMoodService _mood;
    private readonly IScriptureContextEngine _scripture;
    private readonly IPerformanceMonitor _performance;
    private readonly IConversationFlowPredictor _predictor;
    private readonly ConversationState _conversationState;

    public CoreServicesOrchestrator(
        IIntelligentCacheService cache,
        ICharacterMoodService mood,
        IScriptureContextEngine scripture,
        IPerformanceMonitor performance,
        IConversationFlowPredictor predictor)
    {
        _cache = cache;
        _mood = mood;
        _scripture = scripture;
        _performance = performance;
        _predictor = predictor;
        _conversationState = new ConversationState();
    }

    public async Task<EnhancedResponse> GetEnhancedResponseAsync(string characterName, string question)
    {
        using var operation = _performance.BeginOperation("GetEnhancedResponse", "Core");
        var metrics = new PerformanceMetrics();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var overallSw = System.Diagnostics.Stopwatch.StartNew();
        
        var result = new EnhancedResponse();
        
        // Update conversation state
        _conversationState.CharacterName = characterName;
        _conversationState.CurrentTopic = ExtractTopic(question);
        _conversationState.RecentQuestions.Add(question);
        if (_conversationState.RecentQuestions.Count > 10)
        {
            _conversationState.RecentQuestions.RemoveAt(0);
        }
        _conversationState.TurnCount++;
        
        // Try cache first
        sw.Restart();
        var cacheKey = $"{characterName}:{question}";
        var cacheResult = await _cache.GetOrCreateAsync<string?>(
            cacheKey,
            () => Task.FromResult<string?>(null),
            new CacheOptions { EnableSimilarityMatching = true });
        
        metrics.CacheLookupTime = sw.Elapsed;
        
        if (cacheResult.WasHit && !string.IsNullOrEmpty(cacheResult.Value))
        {
            result.Response = cacheResult.Value;
            result.WasCached = true;
            result.CacheConfidence = cacheResult.SimilarityScore;
            metrics.UsedCache = true;
            _performance.RecordMetric("cache_hit_rate", 1.0);
        }
        else
        {
            _performance.RecordMetric("cache_hit_rate", 0.0);
        }
        
        // Get mood in parallel with scripture lookup
        sw.Restart();
        var moodTask = GetMoodEnhancementAsync(characterName, question);
        var scriptureTask = _scripture.GetRelevantScripturesAsync(_conversationState.CurrentTopic, characterName);
        var predictTask = _predictor.PredictNextQuestionsAsync(_conversationState);
        
        await Task.WhenAll(moodTask, scriptureTask, predictTask);
        
        // Apply mood
        result.CurrentMood = await moodTask;
        result.MoodModifier = await _mood.GetMoodInfluencedPromptModifierAsync(characterName);
        metrics.MoodProcessingTime = sw.Elapsed;
        
        // Apply scripture context
        sw.Restart();
        result.ScriptureContext = await scriptureTask;
        metrics.ScriptureTime = sw.Elapsed;
        
        // Get predictions
        sw.Restart();
        result.SuggestedFollowUps = await predictTask;
        metrics.PredictionTime = sw.Elapsed;
        
        overallSw.Stop();
        metrics.TotalTime = overallSw.Elapsed;
        result.Metrics = metrics;
        
        _performance.RecordMetric("response_enhancement_ms", metrics.TotalTime.TotalMilliseconds, "ms");
        
        return result;
    }

    public async Task<ConversationEnhancement> GetConversationEnhancementAsync(string characterName, string topic)
    {
        using var operation = _performance.BeginOperation("GetConversationEnhancement", "Core");
        
        // Update state
        _conversationState.CharacterName = characterName;
        _conversationState.CurrentTopic = topic;
        if (!_conversationState.RecentTopics.Contains(topic))
        {
            _conversationState.RecentTopics.Add(topic);
        }
        
        // Run all enhancements in parallel
        var predictTask = _predictor.PredictNextQuestionsAsync(_conversationState);
        var topicsTask = _predictor.GetSuggestedTopicsAsync(characterName, topic);
        var scriptureTask = _scripture.GetRelevantScripturesAsync(topic, characterName);
        var moodTask = _mood.GetCurrentMoodAsync(characterName);
        var moodModifierTask = _mood.GetMoodInfluencedPromptModifierAsync(characterName);
        var templatesTask = _predictor.GetConversationTemplatesAsync(characterName);
        
        await Task.WhenAll(predictTask, topicsTask, scriptureTask, moodTask, moodModifierTask, templatesTask);
        
        return new ConversationEnhancement
        {
            PredictedQuestions = await predictTask,
            SuggestedTopics = await topicsTask,
            ScriptureContext = await scriptureTask,
            CurrentMood = await moodTask,
            MoodPromptModifier = await moodModifierTask,
            Templates = await templatesTask
        };
    }

    public async Task RecordInteractionAsync(string characterName, string question, string response)
    {
        using var operation = _performance.BeginOperation("RecordInteraction", "Core");
        
        // Cache the response
        var cacheKey = $"{characterName}:{question}";
        await _cache.SetAsync(cacheKey, response, new CacheOptions
        {
            Priority = CachePriority.Normal,
            AbsoluteExpiration = TimeSpan.FromHours(24),
            Tags = new[] { characterName.ToLowerInvariant(), "response" }
        });
        
        // Update mood based on interaction
        var context = new ConversationContext
        {
            Topic = ExtractTopic(question),
            LastUserMessage = question,
            RecentTopics = _conversationState.RecentTopics,
            IsQuestion = question.Contains('?'),
            IsPersonal = question.Contains("my", StringComparison.OrdinalIgnoreCase) ||
                        question.Contains("i ", StringComparison.OrdinalIgnoreCase)
        };
        await _mood.UpdateMoodFromContextAsync(characterName, context);
        
        // Record question pattern
        if (_conversationState.RecentQuestions.Count > 1)
        {
            var prevQuestion = _conversationState.RecentQuestions[^2];
            await _predictor.RecordQuestionAsync(characterName, prevQuestion, question);
        }
        else
        {
            await _predictor.RecordQuestionAsync(characterName, question);
        }
        
        // Log event
        _performance.RecordEvent("Interaction", new Dictionary<string, string>
        {
            ["character"] = characterName,
            ["topic"] = context.Topic,
            ["cached"] = "false"
        });
    }

    public async Task<SystemHealth> GetSystemHealthAsync()
    {
        var health = new SystemHealth();
        
        // Get all health metrics in parallel
        var cacheStats = _cache.GetStatistics();
        var performanceReport = _performance.GetReport(TimeSpan.FromMinutes(30));
        var healthCheck = await _performance.CheckHealthAsync();
        
        health.CacheStats = cacheStats;
        health.PerformanceReport = performanceReport;
        health.HealthCheck = healthCheck;
        health.IsHealthy = healthCheck.IsHealthy && cacheStats.HitRate > 20;
        health.Suggestions = performanceReport.Suggestions;
        
        // Add cache-specific suggestions
        if (cacheStats.HitRate < 30 && cacheStats.TotalHits + cacheStats.TotalMisses > 100)
        {
            health.Suggestions.Add(new OptimizationSuggestion
            {
                Area = "Caching",
                Issue = $"Low cache hit rate ({cacheStats.HitRate:F1}%)",
                Suggestion = "Consider increasing cache duration or enabling similarity matching",
                Impact = "Faster responses, reduced AI calls",
                Priority = Priority.Medium
            });
        }
        
        return health;
    }

    public async Task WarmupAsync()
    {
        using var operation = _performance.BeginOperation("Warmup", "Core");
        
        // Preload common scriptures
        await _scripture.PreloadCommonScripturesAsync();
        
        // Warm up cache with common queries
        var commonQueries = new[]
        {
            "Peter:What is faith?",
            "Paul:Tell me about grace",
            "John:Why is love important?",
            "Moses:What is the meaning of the law?",
            "David:How do you worship?"
        };
        
        await _cache.WarmupAsync(commonQueries);
        
        // Load cache from disk
        await _cache.LoadFromDiskAsync();
        
        _performance.RecordEvent("SystemWarmup", new Dictionary<string, string>
        {
            ["status"] = "complete"
        });
    }

    private async Task<CharacterMood> GetMoodEnhancementAsync(string characterName, string question)
    {
        // Update mood based on question context
        var context = new ConversationContext
        {
            Topic = ExtractTopic(question),
            LastUserMessage = question,
            RecentTopics = _conversationState.RecentTopics,
            IsQuestion = question.Contains('?'),
            IsChallenging = question.Contains("but", StringComparison.OrdinalIgnoreCase) ||
                           question.Contains("however", StringComparison.OrdinalIgnoreCase),
            IsPersonal = question.Contains("my", StringComparison.OrdinalIgnoreCase)
        };
        
        await _mood.UpdateMoodFromContextAsync(characterName, context);
        return await _mood.GetCurrentMoodAsync(characterName);
    }

    private string ExtractTopic(string question)
    {
        var stopWords = new HashSet<string> 
        { 
            "what", "how", "why", "when", "where", "who", "which", "can", "could",
            "would", "should", "do", "does", "did", "is", "are", "was", "were",
            "tell", "me", "about", "the", "a", "an", "your", "you", "please"
        };
        
        var words = question.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Take(3);
        
        return string.Join(" ", words);
    }
}
