using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Tiered hybrid AI service that selects the best AI backend based on device capabilities:
/// - Tier 1: Local Ollama (desktop) or On-device LLamaSharp (capable mobile)
/// - Tier 2: Cloud API (Groq) - best quality when online
/// - Tier 3: Cached responses - emergency fallback for limited devices
/// </summary>
public class HybridAIService : IAIService
{
    private readonly LocalAIService _localService;
    private readonly GroqAIService _cloudService;
    private readonly OnDeviceAIService? _onDeviceService;
    private readonly CachedResponseAIService _cachedService;
    private readonly IDeviceCapabilityService _capabilityService;
    private readonly ILogger<HybridAIService> _logger;
    private readonly ResilienceHelper _resilience;
    private readonly AIBackendRecommendation _recommendation;
    private readonly bool _cloudAvailable;

    public HybridAIService(
        LocalAIService localService,
        GroqAIService cloudService,
        OnDeviceAIService? onDeviceService,
        CachedResponseAIService cachedService,
        IDeviceCapabilityService capabilityService,
        IConfiguration configuration,
        ILogger<HybridAIService> logger)
    {
        _localService = localService;
        _cloudService = cloudService;
        _onDeviceService = onDeviceService;
        _cachedService = cachedService;
        _capabilityService = capabilityService;
        _logger = logger;
        _resilience = new ResilienceHelper(logger, maxRetries: 2, baseDelayMs: 500);
        
        _cloudAvailable = _cloudService.IsAvailable;
        _recommendation = _capabilityService.GetRecommendedBackend();
        
        _logger.LogInformation(
            "HybridAIService initialized. Primary: {Primary}, Fallback: {Fallback}, CloudAvailable: {CloudAvailable}",
            _recommendation.Primary, _recommendation.Fallback, _cloudAvailable);
    }

    public async Task<string> GetChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var hasNetwork = _capabilityService.HasNetworkConnectivity();
        
        // Try primary backend
        try
        {
            return await TryBackendAsync(
                _recommendation.Primary, 
                character, conversationHistory, userMessage, 
                hasNetwork, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary backend {Backend} failed, trying fallback", _recommendation.Primary);
        }

        // Try fallback backend
        try
        {
            return await TryBackendAsync(
                _recommendation.Fallback, 
                character, conversationHistory, userMessage, 
                hasNetwork, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback backend {Backend} failed, using emergency cached responses", _recommendation.Fallback);
        }

        // Emergency: cached responses
        return await _cachedService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
    }

    private async Task<string> TryBackendAsync(
        AIBackendType backend,
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        bool hasNetwork,
        CancellationToken cancellationToken)
    {
        return backend switch
        {
            AIBackendType.LocalOllama => await _localService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken),
            AIBackendType.OnDevice when _onDeviceService != null => await _onDeviceService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken),
            AIBackendType.Cloud when hasNetwork && _cloudAvailable => await _cloudService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken),
            AIBackendType.Cached => await _cachedService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken),
            _ => throw new InvalidOperationException($"Backend {backend} not available")
        };
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var hasNetwork = _capabilityService.HasNetworkConnectivity();
        
        // Determine which backend to use for streaming
        var backend = _recommendation.Primary;
        if (backend == AIBackendType.Cloud && (!hasNetwork || !_cloudAvailable))
        {
            backend = _recommendation.Fallback;
        }
        if (backend == AIBackendType.OnDevice && _onDeviceService == null)
        {
            backend = hasNetwork && _cloudAvailable ? AIBackendType.Cloud : AIBackendType.Cached;
        }

        var errorOccurred = false;
        var errorMessage = "";

        // Try primary streaming
        if (backend == AIBackendType.LocalOllama)
        {
            await foreach (var token in TryLocalStreamAsync(character, conversationHistory, userMessage, cancellationToken))
            {
                if (token.StartsWith("__ERROR__:"))
                {
                    errorOccurred = true;
                    errorMessage = token.Replace("__ERROR__:", "");
                    break;
                }
                yield return token;
            }
        }
        else if (backend == AIBackendType.OnDevice && _onDeviceService != null)
        {
            await foreach (var token in TryOnDeviceStreamAsync(character, conversationHistory, userMessage, cancellationToken))
            {
                if (token.StartsWith("__ERROR__:"))
                {
                    errorOccurred = true;
                    errorMessage = token.Replace("__ERROR__:", "");
                    break;
                }
                yield return token;
            }
        }
        else if (backend == AIBackendType.Cloud && hasNetwork && _cloudAvailable)
        {
            var response = await _cloudService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
            yield return response;
            yield break;
        }
        else
        {
            // Cached fallback
            await foreach (var token in _cachedService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken))
            {
                yield return token;
            }
            yield break;
        }

        // Handle errors by falling back
        if (errorOccurred)
        {
            _logger.LogWarning("Streaming failed: {Error}, falling back", errorMessage);
            
            if (hasNetwork && _cloudAvailable)
            {
                var response = await _cloudService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
                yield return response;
            }
            else
            {
                await foreach (var token in _cachedService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken))
                {
                    yield return token;
                }
            }
        }
    }

    private async IAsyncEnumerable<string> TryLocalStreamAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enumerator = _localService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        
        string? errorMessage = null;
        
        while (true)
        {
            string? current = null;
            bool hasNext = false;
            
            try
            {
                hasNext = await enumerator.MoveNextAsync();
                if (hasNext)
                    current = enumerator.Current;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                break;
            }
            
            if (!hasNext)
                break;
                
            if (current != null)
                yield return current;
        }
        
        if (errorMessage != null)
        {
            yield return $"__ERROR__:{errorMessage}";
        }
    }

    private async IAsyncEnumerable<string> TryOnDeviceStreamAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_onDeviceService == null)
        {
            yield return "__ERROR__:On-device service not available";
            yield break;
        }

        var enumerator = _onDeviceService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        
        string? errorMessage = null;
        
        while (true)
        {
            string? current = null;
            bool hasNext = false;
            
            try
            {
                hasNext = await enumerator.MoveNextAsync();
                if (hasNext)
                    current = enumerator.Current;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                break;
            }
            
            if (!hasNext)
                break;
                
            if (current != null)
                yield return current;
        }
        
        if (errorMessage != null)
        {
            yield return $"__ERROR__:{errorMessage}";
        }
    }

    public async Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default)
    {
        var hasNetwork = _capabilityService.HasNetworkConnectivity();

        // Try primary
        try
        {
            return _recommendation.Primary switch
            {
                AIBackendType.LocalOllama => await _localService.GeneratePrayerAsync(topic, cancellationToken),
                AIBackendType.OnDevice when _onDeviceService != null => await _onDeviceService.GeneratePrayerAsync(topic, cancellationToken),
                AIBackendType.Cloud when hasNetwork && _cloudAvailable => await _cloudService.GeneratePrayerAsync(topic, cancellationToken),
                _ => await _cachedService.GeneratePrayerAsync(topic, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary prayer generation failed");
        }

        // Try fallback
        try
        {
            return _recommendation.Fallback switch
            {
                AIBackendType.LocalOllama => await _localService.GeneratePrayerAsync(topic, cancellationToken),
                AIBackendType.OnDevice when _onDeviceService != null => await _onDeviceService.GeneratePrayerAsync(topic, cancellationToken),
                AIBackendType.Cloud when hasNetwork && _cloudAvailable => await _cloudService.GeneratePrayerAsync(topic, cancellationToken),
                _ => await _cachedService.GeneratePrayerAsync(topic, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback prayer generation failed");
        }

        // Emergency cached
        return await _cachedService.GeneratePrayerAsync(topic, cancellationToken);
    }

    public async Task<string> GeneratePersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken = default)
    {
        var hasNetwork = _capabilityService.HasNetworkConnectivity();

        // Try primary
        try
        {
            return _recommendation.Primary switch
            {
                AIBackendType.LocalOllama => await _localService.GeneratePersonalizedPrayerAsync(options, cancellationToken),
                AIBackendType.OnDevice when _onDeviceService != null => await _onDeviceService.GeneratePersonalizedPrayerAsync(options, cancellationToken),
                AIBackendType.Cloud when hasNetwork && _cloudAvailable => await _cloudService.GeneratePersonalizedPrayerAsync(options, cancellationToken),
                _ => await _cachedService.GeneratePersonalizedPrayerAsync(options, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary personalized prayer generation failed");
        }

        // Try fallback
        try
        {
            return _recommendation.Fallback switch
            {
                AIBackendType.LocalOllama => await _localService.GeneratePersonalizedPrayerAsync(options, cancellationToken),
                AIBackendType.OnDevice when _onDeviceService != null => await _onDeviceService.GeneratePersonalizedPrayerAsync(options, cancellationToken),
                AIBackendType.Cloud when hasNetwork && _cloudAvailable => await _cloudService.GeneratePersonalizedPrayerAsync(options, cancellationToken),
                _ => await _cachedService.GeneratePersonalizedPrayerAsync(options, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback personalized prayer generation failed");
        }

        // Emergency - fall back to simple prayer
        return await _cachedService.GeneratePrayerAsync(options.Topic, cancellationToken);
    }

    public async Task<string> GenerateDevotionalAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var hasNetwork = _capabilityService.HasNetworkConnectivity();

        // Try primary
        try
        {
            return _recommendation.Primary switch
            {
                AIBackendType.LocalOllama => await _localService.GenerateDevotionalAsync(date, cancellationToken),
                AIBackendType.OnDevice when _onDeviceService != null => await _onDeviceService.GenerateDevotionalAsync(date, cancellationToken),
                AIBackendType.Cloud when hasNetwork && _cloudAvailable => await _cloudService.GenerateDevotionalAsync(date, cancellationToken),
                _ => await _cachedService.GenerateDevotionalAsync(date, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary devotional generation failed");
        }

        // Try fallback
        try
        {
            return _recommendation.Fallback switch
            {
                AIBackendType.LocalOllama => await _localService.GenerateDevotionalAsync(date, cancellationToken),
                AIBackendType.OnDevice when _onDeviceService != null => await _onDeviceService.GenerateDevotionalAsync(date, cancellationToken),
                AIBackendType.Cloud when hasNetwork && _cloudAvailable => await _cloudService.GenerateDevotionalAsync(date, cancellationToken),
                _ => await _cachedService.GenerateDevotionalAsync(date, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback devotional generation failed");
        }

        // Emergency cached
        return await _cachedService.GenerateDevotionalAsync(date, cancellationToken);
    }
}
