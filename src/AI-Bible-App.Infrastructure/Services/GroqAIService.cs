using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Groq cloud AI service - ultra-fast inference, free tier available
/// Get your API key at: https://console.groq.com/keys
/// </summary>
public class GroqAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly ILogger<GroqAIService> _logger;
    private const string BaseUrl = "https://api.groq.com/openai/v1/chat/completions";

    public GroqAIService(IConfiguration configuration, ILogger<GroqAIService> logger)
    {
        _logger = logger;
        _apiKey = configuration["Groq:ApiKey"] ?? "";
        _modelName = configuration["Groq:ModelName"] ?? "llama-3.1-8b-instant";
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
        
        _logger.LogInformation("GroqAIService initialized with model: {Model}", _modelName);
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public async Task<string> GetChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Groq API key not configured");

        var messages = new List<GroqMessage>
        {
            new() { Role = "system", Content = character.SystemPrompt }
        };

        foreach (var msg in conversationHistory.TakeLast(10))
        {
            messages.Add(new GroqMessage
            {
                Role = msg.Role == "assistant" ? "assistant" : "user",
                Content = msg.Content
            });
        }

        messages.Add(new GroqMessage { Role = "user", Content = userMessage });

        var request = new GroqRequest
        {
            Model = _modelName,
            Messages = messages,
            MaxTokens = 1024,
            Temperature = 0.7
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(BaseUrl, request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: cancellationToken);
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response received";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq API error");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For simplicity, use non-streaming and yield the full response
        var response = await GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
        yield return response;
    }

    public async Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Groq API key not configured");

        var messages = new List<GroqMessage>
        {
            new() 
            { 
                Role = "system", 
                Content = @"You are a compassionate prayer writer. Generate heartfelt, biblical prayers that are meaningful and spiritually uplifting.

IMPORTANT GUIDELINES:
- Keep prayers concise (2-3 short paragraphs)
- Use natural, sincere language that flows like a real conversation with God
- Do NOT include parenthetical references like (Psalm 23:1) or (Matthew 6:9) in the prayer
- Do NOT include any meta-commentary, instructions, or notes
- Do NOT use overly flowery or verbose language
- Let scripture themes inspire the prayer naturally without citing chapter and verse
- Write the prayer directly - start with addressing God and end with Amen
- The output should ONLY be the prayer text itself, nothing else"
            },
            new() 
            { 
                Role = "user", 
                Content = $"Please write a thoughtful prayer about: {topic}"
            }
        };

        var request = new GroqRequest
        {
            Model = _modelName,
            Messages = messages,
            MaxTokens = 512,
            Temperature = 0.8
        };

        var response = await _httpClient.PostAsJsonAsync(BaseUrl, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: cancellationToken);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "Prayer could not be generated";
    }

    public async Task<string> GeneratePersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Groq API key not configured");

        // Build personalized prompt
        var promptParts = new List<string>();
        
        if (options.RequestType != PrayerRequestType.General)
            promptParts.Add($"Write {options.RequestType.GetDescription()}.");
        
        promptParts.Add(options.Tradition.GetStyleHint());
        
        if (options.Mood.HasValue)
            promptParts.Add($"The person praying is {options.Mood.Value.GetDescription()}.");
        
        if (options.TimeContext.HasValue)
            promptParts.Add($"This prayer is {options.TimeContext.Value.GetTimeGreeting()}.");
        
        if (!string.IsNullOrEmpty(options.LifeCircumstances))
            promptParts.Add($"Life context: {options.LifeCircumstances}");
        
        if (options.Intentions.Any())
            promptParts.Add($"Include these intentions: {string.Join(", ", options.Intentions)}");
        
        if (!string.IsNullOrEmpty(options.PrayingFor))
            promptParts.Add($"This prayer is for/about: {options.PrayingFor}");
        
        var wordCount = options.Length.GetWordCount();
        promptParts.Add($"Keep the prayer approximately {wordCount} words.");

        var messages = new List<GroqMessage>
        {
            new() 
            { 
                Role = "system", 
                Content = $@"You are a compassionate prayer writer. Generate heartfelt, biblically-grounded prayers.

{string.Join("\n", promptParts)}

IMPORTANT GUIDELINES:
- Write the prayer directly - start with addressing God and end with Amen
- Use natural, sincere language that flows like a real conversation with God
- Do NOT include parenthetical references or meta-commentary
- The output should ONLY be the prayer text itself"
            },
            new() 
            { 
                Role = "user", 
                Content = string.IsNullOrEmpty(options.Topic) 
                    ? "Generate a personalized prayer." 
                    : $"Generate a prayer about: {options.Topic}"
            }
        };

        var request = new GroqRequest
        {
            Model = _modelName,
            Messages = messages,
            MaxTokens = wordCount * 2,
            Temperature = 0.8
        };

        var response = await _httpClient.PostAsJsonAsync(BaseUrl, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: cancellationToken);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "Prayer could not be generated";
    }

    public async Task<string> GenerateDevotionalAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Groq API key not configured");

        var messages = new List<GroqMessage>
        {
            new() 
            { 
                Role = "system", 
                Content = @"You are a devotional writer. Generate a complete devotional as valid JSON with these exact fields:
{""title"": ""5-8 word title"", ""scripture"": ""full verse text"", ""scriptureReference"": ""Book Chapter:Verse"", ""content"": ""150-200 word reflection"", ""prayer"": ""50-75 word prayer"", ""category"": ""Faith/Hope/Love/Wisdom/Strength/Grace/Peace/Joy""}
Output ONLY the JSON object, no markdown."
            },
            new() 
            { 
                Role = "user", 
                Content = $"Create a daily devotional for {date:MMMM d, yyyy}. Return only valid JSON."
            }
        };

        var request = new GroqRequest
        {
            Model = _modelName,
            Messages = messages,
            MaxTokens = 1024,
            Temperature = 0.7
        };

        var response = await _httpClient.PostAsJsonAsync(BaseUrl, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: cancellationToken);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "{}";
    }

    // Request/Response DTOs
    private class GroqRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
        
        [JsonPropertyName("messages")]
        public List<GroqMessage> Messages { get; set; } = new();
        
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
        
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private class GroqMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class GroqResponse
    {
        [JsonPropertyName("choices")]
        public List<GroqChoice>? Choices { get; set; }
    }

    private class GroqChoice
    {
        [JsonPropertyName("message")]
        public GroqMessage? Message { get; set; }
    }
}
