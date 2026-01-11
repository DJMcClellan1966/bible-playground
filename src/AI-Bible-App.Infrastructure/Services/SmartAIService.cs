using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Core.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Smart AI service that automatically switches between online and offline modes
/// Provides seamless fallback like GPT4All
/// </summary>
public class SmartAIService : IAIService
{
    private readonly IAIService _onlineService;
    private readonly IOfflineAIService _offlineService;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<SmartAIService> _logger;
    private bool _preferOfflineMode = false;

    public SmartAIService(
        IAIService onlineService,
        IOfflineAIService offlineService,
        IConnectivityService connectivity,
        ILogger<SmartAIService> logger)
    {
        _onlineService = onlineService;
        _offlineService = offlineService;
        _connectivity = connectivity;
        _logger = logger;

        // Monitor connectivity changes
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Set whether to prefer offline mode even when online
    /// </summary>
    public void SetOfflinePreference(bool preferOffline)
    {
        _preferOfflineMode = preferOffline;
        _logger.LogInformation("Offline preference set to: {PreferOffline}", preferOffline);
    }

    public async Task<string> GetChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var useOffline = await ShouldUseOfflineModeAsync();

        try
        {
            if (useOffline)
            {
                _logger.LogInformation("Using offline AI for character {Character}", character.Name);
                return await GetOfflineResponseAsync(character, conversationHistory, userMessage, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Using online AI for character {Character}", character.Name);
                return await _onlineService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary mode failed, attempting fallback");

            // Fallback to opposite mode
            if (useOffline)
            {
                // Try online as fallback
                if (_connectivity.IsConnected)
                {
                    _logger.LogInformation("Falling back to online mode");
                    return await _onlineService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
                }
                else
                {
                    return "I apologize, but I'm currently unable to respond. Please ensure you have downloaded an offline model or check your internet connection.";
                }
            }
            else
            {
                // Try offline as fallback
                if (await _offlineService.IsOfflineModeAvailableAsync())
                {
                    _logger.LogInformation("Falling back to offline mode");
                    return await GetOfflineResponseAsync(character, conversationHistory, userMessage, cancellationToken);
                }
                else
                {
                    throw; // Re-throw if no fallback available
                }
            }
        }
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var useOffline = await ShouldUseOfflineModeAsync();

        if (useOffline)
        {
            _logger.LogInformation("Streaming offline AI for character {Character}", character.Name);
            
            var systemPrompt = BuildSystemPrompt(character);
            await foreach (var token in _offlineService.GetStreamingCompletionAsync(systemPrompt, userMessage, cancellationToken))
            {
                yield return token;
            }
        }
        else
        {
            _logger.LogInformation("Streaming online AI for character {Character}", character.Name);
            
            await foreach (var token in _onlineService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken))
            {
                yield return token;
            }
        }
    }

    private async Task<bool> ShouldUseOfflineModeAsync()
    {
        // User preference for offline mode
        if (_preferOfflineMode)
        {
            var offlineAvailable = await _offlineService.IsOfflineModeAvailableAsync();
            if (offlineAvailable)
                return true;
        }

        // No internet connection - must use offline
        if (!_connectivity.IsConnected)
        {
            return await _offlineService.IsOfflineModeAvailableAsync();
        }

        // Online and no preference for offline
        return false;
    }

    private async Task<string> GetOfflineResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt(character);
        
        // Optimize context for local models (they have smaller context windows)
        var contextualMessage = BuildContextualMessage(conversationHistory, userMessage);
        
        return await _offlineService.GetCompletionAsync(systemPrompt, contextualMessage, cancellationToken);
    }

    public async Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default)
    {
        var useOffline = await ShouldUseOfflineModeAsync();

        try
        {
            if (useOffline)
            {
                _logger.LogInformation("Generating prayer offline for topic: {Topic}", topic);
                
                var systemPrompt = "You are a spiritual guide helping to compose heartfelt prayers. Create prayers that are warm, sincere, and biblically grounded.";
                var userMessage = $"Please help me write a prayer about: {topic}";
                
                return await _offlineService.GetCompletionAsync(systemPrompt, userMessage, cancellationToken);
            }
            else
            {
                return await _onlineService.GeneratePrayerAsync(topic, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prayer generation failed, attempting fallback");

            // Fallback
            if (useOffline)
            {
                if (_connectivity.IsConnected)
                {
                    return await _onlineService.GeneratePrayerAsync(topic, cancellationToken);
                }
            }
            else
            {
                if (await _offlineService.IsOfflineModeAvailableAsync())
                {
                    var systemPrompt = "You are a spiritual guide helping to compose heartfelt prayers. Create prayers that are warm, sincere, and biblically grounded.";
                    var userMessage = $"Please help me write a prayer about: {topic}";
                    return await _offlineService.GetCompletionAsync(systemPrompt, userMessage, cancellationToken);
                }
            }
            
            throw;
        }
    }

    public async Task<string> GeneratePersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken = default)
    {
        var useOffline = await ShouldUseOfflineModeAsync();

        try
        {
            if (useOffline)
            {
                _logger.LogInformation("Generating personalized prayer offline");
                
                var promptParts = new List<string>();
                if (options.RequestType != PrayerRequestType.General)
                    promptParts.Add($"Write {options.RequestType.GetDescription()}.");
                if (options.Mood.HasValue)
                    promptParts.Add($"The person is {options.Mood.Value.GetDescription()}.");
                if (!string.IsNullOrEmpty(options.PrayingFor))
                    promptParts.Add($"Praying for: {options.PrayingFor}");
                    
                var systemPrompt = $"You are a spiritual guide helping to compose heartfelt prayers. {string.Join(" ", promptParts)}";
                var userMessage = $"Please help me write a prayer about: {options.Topic}";
                
                return await _offlineService.GetCompletionAsync(systemPrompt, userMessage, cancellationToken);
            }
            else
            {
                return await _onlineService.GeneratePersonalizedPrayerAsync(options, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Personalized prayer generation failed, attempting fallback");

            // Fallback to simple prayer
            return await GeneratePrayerAsync(options.Topic, cancellationToken);
        }
    }

    public async Task<string> GenerateDevotionalAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var useOffline = await ShouldUseOfflineModeAsync();

        try
        {
            if (useOffline)
            {
                _logger.LogInformation("Generating devotional offline for date: {Date}", date);
                
                var systemPrompt = @"You are a devotional writer. Generate a daily devotional as valid JSON with these exact fields:
{""title"": ""title"", ""scripture"": ""verse"", ""scriptureReference"": ""reference"", ""content"": ""reflection"", ""prayer"": ""prayer"", ""category"": ""Faith/Hope/Love/Wisdom/Strength/Grace/Peace/Joy""}";
                var userMessage = $"Create a devotional for {date:MMMM d, yyyy}. Return only JSON.";
                
                return await _offlineService.GetCompletionAsync(systemPrompt, userMessage, cancellationToken);
            }
            else
            {
                return await _onlineService.GenerateDevotionalAsync(date, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Devotional generation failed, attempting fallback");

            // Fallback
            if (useOffline && _connectivity.IsConnected)
            {
                return await _onlineService.GenerateDevotionalAsync(date, cancellationToken);
            }
            else if (!useOffline && await _offlineService.IsOfflineModeAvailableAsync())
            {
                var systemPrompt = @"You are a devotional writer. Generate a daily devotional as valid JSON.";
                var userMessage = $"Create a devotional for {date:MMMM d, yyyy}. Return only JSON.";
                return await _offlineService.GetCompletionAsync(systemPrompt, userMessage, cancellationToken);
            }
            
            throw;
        }
    }

    private string BuildSystemPrompt(BiblicalCharacter character)
    {
        // Optimized prompt for local models - shorter but effective
        return $@"You are {character.Name}, a biblical character. 

Background: {character.Description}

Era: {character.Era}

Speaking style: Use wisdom and insight from your biblical experiences.

Respond authentically as {character.Name} would, drawing from biblical wisdom and your life experiences. Keep responses focused and conversational (2-4 paragraphs). Be warm, encouraging, and spiritually insightful.";
    }

    private string BuildContextualMessage(List<ChatMessage> conversationHistory, string userMessage)
    {
        // For local models, only include last 3 messages for context to save tokens
        var recentMessages = conversationHistory.TakeLast(3).ToList();
        
        if (recentMessages.Count == 0)
        {
            return userMessage;
        }

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Recent conversation:");
        
        foreach (var msg in recentMessages)
        {
            var role = msg.Role == "user" ? "User" : "Assistant";
            contextBuilder.AppendLine($"{role}: {msg.Content}");
        }
        
        contextBuilder.AppendLine();
        contextBuilder.AppendLine($"User: {userMessage}");
        
        return contextBuilder.ToString();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        _logger.LogInformation("Connectivity changed: IsConnected={IsConnected}, Type={Type}", 
            e.IsConnected, e.ConnectionType);
    }
}
