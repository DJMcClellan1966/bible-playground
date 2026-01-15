using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Simple hybrid AI service that tries local Ollama first with timeout,
/// then falls back to Groq cloud for fast responses.
/// </summary>
public class HybridAIServiceSimple : IAIService
{
    private readonly LocalAIService _localService;
    private readonly GroqAIService _groqService;
    private readonly CachedResponseAIService _cachedService;
    private readonly IUserService _userService;
    private readonly ILogger<HybridAIServiceSimple> _logger;
    private readonly bool _preferLocal;
    private readonly TimeSpan _localTimeout;
    private readonly bool _groqAvailable;

    public HybridAIServiceSimple(
        LocalAIService localService,
        GroqAIService groqService,
        CachedResponseAIService cachedService,
        IUserService userService,
        IConfiguration configuration,
        ILogger<HybridAIServiceSimple> logger)
    {
        _localService = localService;
        _groqService = groqService;
        _cachedService = cachedService;
        _userService = userService;
        _logger = logger;
        
        _preferLocal = configuration["AI:PreferLocal"] != "false";
        _groqAvailable = _groqService.IsAvailable;
        
        // Timeout for local before trying cloud (20 seconds default)
        var timeoutSeconds = int.TryParse(configuration["AI:LocalTimeoutSeconds"], out var t) ? t : 20;
        _localTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        
        _logger.LogInformation("HybridAIServiceSimple initialized. PreferLocal={PreferLocal}, GroqAvailable={GroqAvailable}, LocalTimeout={Timeout}s",
            _preferLocal, _groqAvailable, timeoutSeconds);
    }

    public async Task<string> GetChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var preference = GetPreferredBackend();

        if (preference == AiBackendPreference.Cloud)
        {
            var cloudResponse = await TryGroqAsync(character, conversationHistory, userMessage, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;

            var localResponse = await TryLocalAsync(character, conversationHistory, userMessage, cancellationToken);
            if (localResponse != null)
                return localResponse;
        }
        else
        {
            var localResponse = await TryLocalAsync(character, conversationHistory, userMessage, cancellationToken);
            if (localResponse != null)
                return localResponse;

            var cloudResponse = await TryGroqAsync(character, conversationHistory, userMessage, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;
        }

        return await _cachedService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var preference = GetPreferredBackend();

        if (preference == AiBackendPreference.Cloud && _groqAvailable)
        {
            await foreach (var chunk in _groqService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken))
            {
                yield return chunk;
            }
            yield break;
        }

        // Try local first with timeout
        if (preference != AiBackendPreference.Cloud && ShouldPreferLocal())
        {
            Exception? localError = null;
            using var timeoutCts = new CancellationTokenSource(_localTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var localEnumerator = _localService.StreamChatResponseAsync(character, conversationHistory, userMessage, linkedCts.Token).GetAsyncEnumerator(linkedCts.Token);
            
            while (true)
            {
                string? chunk = null;
                try
                {
                    if (!await localEnumerator.MoveNextAsync())
                        break;
                    chunk = localEnumerator.Current;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Local AI stream timed out, falling back to Groq");
                    localError = new TimeoutException();
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Local AI stream failed, falling back to Groq");
                    localError = ex;
                    break;
                }
                
                if (chunk != null)
                    yield return chunk;
            }
            
            await localEnumerator.DisposeAsync();
            
            if (localError == null)
                yield break;
        }

        // Fallback to Groq (non-streaming, yields full response)
        if (_groqAvailable)
        {
            await foreach (var chunk in _groqService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken))
            {
                yield return chunk;
            }
            yield break;
        }

        // Final fallback
        await foreach (var chunk in _cachedService.StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default)
    {
        var preference = GetPreferredBackend();

        if (preference == AiBackendPreference.Cloud)
        {
            var cloudResponse = await TryGroqPrayerAsync(topic, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;

            var localResponse = await TryLocalPrayerAsync(topic, cancellationToken);
            if (localResponse != null)
                return localResponse;
        }
        else if (preference == AiBackendPreference.Local)
        {
            var localResponse = await TryLocalPrayerAsync(topic, cancellationToken);
            if (localResponse != null)
                return localResponse;

            var cloudResponse = await TryGroqPrayerAsync(topic, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;
        }
        else
        {
            if (ShouldPreferLocal())
            {
                var localResponse = await TryLocalPrayerAsync(topic, cancellationToken);
                if (localResponse != null)
                    return localResponse;
            }

            var cloudResponse = await TryGroqPrayerAsync(topic, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;

            var localFallback = await TryLocalPrayerAsync(topic, cancellationToken);
            if (localFallback != null)
                return localFallback;
        }

        return await _cachedService.GeneratePrayerAsync(topic, cancellationToken);
    }

    public async Task<string> GeneratePersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken = default)
    {
        var preference = GetPreferredBackend();

        if (preference == AiBackendPreference.Cloud)
        {
            var cloudResponse = await TryGroqPersonalizedPrayerAsync(options, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;

            var localResponse = await TryLocalPersonalizedPrayerAsync(options, cancellationToken);
            if (localResponse != null)
                return localResponse;
        }
        else if (preference == AiBackendPreference.Local)
        {
            var localResponse = await TryLocalPersonalizedPrayerAsync(options, cancellationToken);
            if (localResponse != null)
                return localResponse;

            var cloudResponse = await TryGroqPersonalizedPrayerAsync(options, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;
        }
        else
        {
            if (ShouldPreferLocal())
            {
                var localResponse = await TryLocalPersonalizedPrayerAsync(options, cancellationToken);
                if (localResponse != null)
                    return localResponse;
            }

            var cloudResponse = await TryGroqPersonalizedPrayerAsync(options, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;

            var localFallback = await TryLocalPersonalizedPrayerAsync(options, cancellationToken);
            if (localFallback != null)
                return localFallback;
        }

        return await _cachedService.GeneratePersonalizedPrayerAsync(options, cancellationToken);
    }

    private bool ShouldPreferLocal()
    {
        if (!_preferLocal)
            return false;

        var preference = GetPreferredBackend();
        return preference == AiBackendPreference.Auto || preference == AiBackendPreference.Local;
    }

    private AiBackendPreference GetPreferredBackend()
    {
        var preferred = _userService.CurrentUser?.Settings.PreferredAIBackend?.ToLowerInvariant();
        return preferred switch
        {
            "local" => AiBackendPreference.Local,
            "cloud" => AiBackendPreference.Cloud,
            _ => AiBackendPreference.Auto
        };
    }

    private async Task<string?> TryLocalAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (!ShouldPreferLocal())
            return null;

        try
        {
            using var timeoutCts = new CancellationTokenSource(_localTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            _logger.LogDebug("Trying local AI service with {Timeout}s timeout", _localTimeout.TotalSeconds);
            var response = await _localService.GetChatResponseAsync(character, conversationHistory, userMessage, linkedCts.Token);
            _logger.LogDebug("Local AI responded successfully");
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Local AI timed out after {Timeout}s, falling back", _localTimeout.TotalSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local AI failed, falling back");
            return null;
        }
    }

    private async Task<string?> TryGroqAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (!_groqAvailable)
            return null;

        try
        {
            _logger.LogInformation("Using Groq cloud service for response");
            return await _groqService.GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq failed, falling back");
            return null;
        }
    }

    private async Task<string?> TryGroqPrayerAsync(string topic, CancellationToken cancellationToken)
    {
        if (!_groqAvailable)
            return null;

        try
        {
            _logger.LogDebug("Using Groq for prayer generation");
            return await _groqService.GeneratePrayerAsync(topic, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Groq prayer failed");
            return null;
        }
    }

    private async Task<string?> TryLocalPrayerAsync(string topic, CancellationToken cancellationToken)
    {
        try
        {
            return await _localService.GeneratePrayerAsync(topic, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local prayer failed");
            return null;
        }
    }

    private async Task<string?> TryGroqPersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken)
    {
        if (!_groqAvailable)
            return null;

        try
        {
            return await _groqService.GeneratePersonalizedPrayerAsync(options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Groq personalized prayer failed");
            return null;
        }
    }

    private async Task<string?> TryLocalPersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken)
    {
        try
        {
            return await _localService.GeneratePersonalizedPrayerAsync(options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local personalized prayer failed");
            return null;
        }
    }

    public async Task<string> GenerateDevotionalAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var preference = GetPreferredBackend();

        if (preference == AiBackendPreference.Cloud)
        {
            var cloudResponse = await TryGroqDevotionalAsync(date, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;

            var localResponse = await TryLocalDevotionalAsync(date, cancellationToken);
            if (localResponse != null)
                return localResponse;
        }
        else if (preference == AiBackendPreference.Local)
        {
            var localResponse = await TryLocalDevotionalAsync(date, cancellationToken);
            if (localResponse != null)
                return localResponse;

            var cloudResponse = await TryGroqDevotionalAsync(date, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;
        }
        else
        {
            if (ShouldPreferLocal())
            {
                var localResponse = await TryLocalDevotionalAsync(date, cancellationToken);
                if (localResponse != null)
                    return localResponse;
            }

            var cloudResponse = await TryGroqDevotionalAsync(date, cancellationToken);
            if (cloudResponse != null)
                return cloudResponse;

            var localFallback = await TryLocalDevotionalAsync(date, cancellationToken);
            if (localFallback != null)
                return localFallback;
        }

        return await _cachedService.GenerateDevotionalAsync(date, cancellationToken);
    }

    private async Task<string?> TryGroqDevotionalAsync(DateTime date, CancellationToken cancellationToken)
    {
        if (!_groqAvailable)
            return null;

        try
        {
            return await _groqService.GenerateDevotionalAsync(date, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Groq devotional failed");
            return null;
        }
    }

    private async Task<string?> TryLocalDevotionalAsync(DateTime date, CancellationToken cancellationToken)
    {
        try
        {
            return await _localService.GenerateDevotionalAsync(date, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local devotional failed");
            return null;
        }
    }
}

public enum AiBackendPreference
{
    Auto,
    Local,
    Cloud
}
