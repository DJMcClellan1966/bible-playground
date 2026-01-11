using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.Services.Core;
using System.Collections.ObjectModel;

namespace AI_Bible_App.Maui.Services;

/// <summary>
/// Integration helper that enhances chat interactions using core optimization services.
/// Designed to work alongside existing ChatViewModel without breaking changes.
/// </summary>
public interface IChatEnhancementService
{
    Task<ChatEnhancement> EnhanceBeforeResponseAsync(BiblicalCharacter character, string userMessage, IEnumerable<ChatMessage> history);
    Task<ChatEnhancement> EnhanceAfterResponseAsync(BiblicalCharacter character, string userMessage, string response);
    Task<List<string>> GetSuggestedQuestionsAsync(BiblicalCharacter character, string? currentTopic = null);
    Task<CharacterMood> GetCharacterMoodAsync(string characterName);
    string GetMoodBasedStyling(CharacterMood mood);
    Task WarmupAsync();
}

public class ChatEnhancement
{
    public string? CachedResponse { get; set; }
    public bool HasCachedResponse => !string.IsNullOrEmpty(CachedResponse);
    public double CacheConfidence { get; set; }
    public CharacterMood CharacterMood { get; set; } = new();
    public string PromptModifier { get; set; } = "";
    public ScriptureContext ScriptureContext { get; set; } = new();
    public List<PredictedQuestion> SuggestedFollowUps { get; set; } = new();
    public List<string> RelevantVerses { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
}

public class ChatEnhancementService : IChatEnhancementService
{
    private readonly ICoreServicesOrchestrator _orchestrator;
    private readonly IIntelligentCacheService _cache;
    private readonly ICharacterMoodService _moodService;
    private readonly IScriptureContextEngine _scriptureEngine;
    private readonly IConversationFlowPredictor _predictor;
    private readonly IPerformanceMonitor _performance;

    public ChatEnhancementService(
        ICoreServicesOrchestrator orchestrator,
        IIntelligentCacheService cache,
        ICharacterMoodService moodService,
        IScriptureContextEngine scriptureEngine,
        IConversationFlowPredictor predictor,
        IPerformanceMonitor performance)
    {
        _orchestrator = orchestrator;
        _cache = cache;
        _moodService = moodService;
        _scriptureEngine = scriptureEngine;
        _predictor = predictor;
        _performance = performance;
    }

    public async Task<ChatEnhancement> EnhanceBeforeResponseAsync(
        BiblicalCharacter character, 
        string userMessage, 
        IEnumerable<ChatMessage> history)
    {
        using var operation = _performance.BeginOperation("ChatEnhancement.Before", "Chat");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var enhancement = new ChatEnhancement();

        try
        {
            // Run enhancements in parallel for speed
            var cacheTask = TryGetCachedResponseAsync(character.Name, userMessage);
            var moodTask = _moodService.GetCurrentMoodAsync(character.Name);
            var modifierTask = _moodService.GetMoodInfluencedPromptModifierAsync(character.Name);
            var scriptureTask = GetRelevantScripturesAsync(character.Name, userMessage);
            var predictTask = GetPredictionsAsync(character.Name, userMessage, history);

            await Task.WhenAll(cacheTask, moodTask, modifierTask, scriptureTask, predictTask);

            // Apply cached response if found
            var cacheResult = await cacheTask;
            if (cacheResult.HasValue)
            {
                enhancement.CachedResponse = cacheResult.Response;
                enhancement.CacheConfidence = cacheResult.Confidence;
            }

            // Apply mood
            enhancement.CharacterMood = await moodTask;
            enhancement.PromptModifier = await modifierTask;

            // Apply scripture context
            var scriptureContext = await scriptureTask;
            enhancement.ScriptureContext = scriptureContext;
            enhancement.RelevantVerses = scriptureContext.PrimaryPassages
                .Select(v => $"{v.Reference}: {v.Text}")
                .ToList();

            // Apply predictions
            enhancement.SuggestedFollowUps = await predictTask;

            // Update mood based on conversation context
            var context = ExtractContext(userMessage, history);
            await _moodService.UpdateMoodFromContextAsync(character.Name, context);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatEnhancement] Error: {ex.Message}");
            _performance.RecordEvent("Enhancement.Error", new Dictionary<string, string>
            {
                ["error"] = ex.Message,
                ["phase"] = "before"
            });
        }

        sw.Stop();
        enhancement.ProcessingTime = sw.Elapsed;
        _performance.RecordMetric("enhancement_before_ms", sw.Elapsed.TotalMilliseconds, "ms");

        return enhancement;
    }

    public async Task<ChatEnhancement> EnhanceAfterResponseAsync(
        BiblicalCharacter character,
        string userMessage,
        string response)
    {
        using var operation = _performance.BeginOperation("ChatEnhancement.After", "Chat");
        var enhancement = new ChatEnhancement();

        try
        {
            // Cache the response for future use
            var cacheKey = $"{character.Name}:{userMessage}";
            await _cache.SetAsync(cacheKey, response, new CacheOptions
            {
                Priority = CachePriority.Normal,
                AbsoluteExpiration = TimeSpan.FromHours(48),
                EnableSimilarityMatching = true,
                Tags = new[] { character.Name.ToLowerInvariant(), "chat-response" }
            });

            // Record the interaction for prediction improvements
            await _predictor.RecordQuestionAsync(character.Name, userMessage);

            // Get updated suggestions for follow-up
            var state = new ConversationState
            {
                CharacterName = character.Name,
                CurrentTopic = ExtractTopic(userMessage),
                TurnCount = 1
            };
            state.RecentQuestions.Add(userMessage);
            
            enhancement.SuggestedFollowUps = await _predictor.PredictNextQuestionsAsync(state);

            // Update mood based on response
            var context = new ConversationContext
            {
                Topic = ExtractTopic(userMessage),
                LastUserMessage = userMessage,
                IsQuestion = userMessage.Contains('?')
            };
            await _moodService.UpdateMoodFromContextAsync(character.Name, context);
            enhancement.CharacterMood = await _moodService.GetCurrentMoodAsync(character.Name);

            _performance.RecordEvent("Interaction.Complete", new Dictionary<string, string>
            {
                ["character"] = character.Name,
                ["cached"] = "false"
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatEnhancement] Post-response error: {ex.Message}");
        }

        return enhancement;
    }

    public async Task<List<string>> GetSuggestedQuestionsAsync(BiblicalCharacter character, string? currentTopic = null)
    {
        try
        {
            var topics = await _predictor.GetSuggestedTopicsAsync(character.Name, currentTopic ?? "general");
            return topics.Take(5).ToList();
        }
        catch
        {
            return new List<string>
            {
                "Tell me about your journey of faith",
                "What wisdom do you have for difficult times?",
                "How do you experience God's presence?",
                "What lessons have shaped your life?",
                "How can I deepen my faith?"
            };
        }
    }

    public async Task<CharacterMood> GetCharacterMoodAsync(string characterName)
    {
        return await _moodService.GetCurrentMoodAsync(characterName);
    }

    public string GetMoodBasedStyling(CharacterMood mood)
    {
        // Return CSS-like styling hints based on mood
        return mood.PrimaryMood switch
        {
            MoodState.Joyful => "color:#FFD700;animation:glow",
            MoodState.Contemplative => "color:#6B8E23;animation:pulse-slow",
            MoodState.Passionate => "color:#DC143C;animation:pulse-fast",
            MoodState.Compassionate => "color:#4169E1;animation:warm",
            MoodState.Solemn => "color:#9370DB;animation:fade",
            MoodState.Teaching => "color:#8B4513;animation:steady",
            MoodState.Grieving => "color:#708090;animation:gentle",
            MoodState.Encouraging => "color:#32CD32;animation:bounce",
            MoodState.Prophetic => "color:#FF6347;animation:pulse",
            MoodState.Challenging => "color:#B8860B;animation:steady",
            _ => "color:#696969;animation:none"
        };
    }

    public async Task WarmupAsync()
    {
        await _orchestrator.WarmupAsync();
    }

    private async Task<(bool HasValue, string? Response, double Confidence)> TryGetCachedResponseAsync(
        string characterName, string userMessage)
    {
        try
        {
            var cacheKey = $"{characterName}:{userMessage}";
            var result = await _cache.GetOrCreateAsync<string?>(
                cacheKey,
                () => Task.FromResult<string?>(null),
                new CacheOptions { EnableSimilarityMatching = true });

            if (result.WasHit && !string.IsNullOrEmpty(result.Value))
            {
                return (true, result.Value, result.SimilarityScore);
            }
        }
        catch { }

        return (false, null, 0);
    }

    private async Task<ScriptureContext> GetRelevantScripturesAsync(string characterName, string message)
    {
        var topic = ExtractTopic(message);
        return await _scriptureEngine.GetRelevantScripturesAsync(topic, characterName);
    }

    private async Task<List<PredictedQuestion>> GetPredictionsAsync(
        string characterName, 
        string message, 
        IEnumerable<ChatMessage> history)
    {
        var state = new ConversationState
        {
            CharacterName = characterName,
            CurrentTopic = ExtractTopic(message),
            TurnCount = history.Count()
        };

        foreach (var msg in history.Where(m => m.Role == "user").TakeLast(5))
        {
            state.RecentQuestions.Add(msg.Content);
        }

        return await _predictor.PredictNextQuestionsAsync(state);
    }

    private ConversationContext ExtractContext(string message, IEnumerable<ChatMessage> history)
    {
        return new ConversationContext
        {
            Topic = ExtractTopic(message),
            LastUserMessage = message,
            RecentTopics = history
                .Where(m => m.Role == "user")
                .TakeLast(3)
                .Select(m => ExtractTopic(m.Content))
                .ToList(),
            IsQuestion = message.Contains('?'),
            IsPersonal = message.Contains("my", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains(" I ", StringComparison.OrdinalIgnoreCase),
            IsChallenging = message.Contains("but", StringComparison.OrdinalIgnoreCase) ||
                           message.Contains("why", StringComparison.OrdinalIgnoreCase)
        };
    }

    private string ExtractTopic(string message)
    {
        var stopWords = new HashSet<string>
        {
            "what", "how", "why", "when", "where", "who", "which", "can", "could",
            "would", "should", "do", "does", "did", "is", "are", "was", "were",
            "tell", "me", "about", "the", "a", "an", "your", "you", "please", "i"
        };

        var words = message.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Take(3);

        return string.Join(" ", words);
    }
}
