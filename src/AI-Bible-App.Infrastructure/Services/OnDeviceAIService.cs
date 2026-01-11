using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// On-device AI service using LLamaSharp for offline inference on mobile devices.
/// Supports tiny quantized models (Qwen2-0.5B, TinyLlama) for low-RAM devices.
/// </summary>
public class OnDeviceAIService : IAIService, IDisposable
{
    private readonly ILogger<OnDeviceAIService> _logger;
    private readonly string _modelPath;
    private readonly int _contextSize;
    private readonly int _gpuLayers;
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private bool _isInitialized;
    private bool _disposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public bool IsAvailable => File.Exists(_modelPath);
    public bool IsInitialized => _isInitialized;

    public OnDeviceAIService(IConfiguration configuration, ILogger<OnDeviceAIService> logger)
    {
        _logger = logger;
        
        // Get model path from config or use default
        var modelDir = configuration["AI:OnDevice:ModelDirectory"] 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-Bible-App", "models");
        
        var modelName = configuration["AI:OnDevice:ModelName"] ?? "qwen2-0.5b-instruct-q4_k_m.gguf";
        _modelPath = Path.Combine(modelDir, modelName);
        
        // Context size - smaller for low RAM devices
        _contextSize = int.TryParse(configuration["AI:OnDevice:ContextSize"], out var ctx) ? ctx : 1024;
        
        // GPU layers - 0 for CPU-only (older iOS devices)
        _gpuLayers = int.TryParse(configuration["AI:OnDevice:GpuLayers"], out var gpu) ? gpu : 0;
        
        _logger.LogInformation("OnDeviceAIService configured. Model: {ModelPath}, Context: {Context}, Available: {Available}",
            _modelPath, _contextSize, IsAvailable);
    }

    /// <summary>
    /// Initialize the model lazily when first needed
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            
            if (!IsAvailable)
            {
                _logger.LogWarning("Model file not found: {ModelPath}", _modelPath);
                return;
            }

            _logger.LogInformation("Loading on-device model: {ModelPath}", _modelPath);
            
            var parameters = new ModelParams(_modelPath)
            {
                ContextSize = (uint)_contextSize,
                GpuLayerCount = _gpuLayers,
                UseMemorymap = true, // Reduces RAM usage
                UseMemoryLock = false
            };

            _model = await Task.Run(() => LLamaWeights.LoadFromFile(parameters));
            _context = _model.CreateContext(parameters);
            _isInitialized = true;
            
            _logger.LogInformation("On-device model loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load on-device model");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> GetChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var response = new StringBuilder();
        await foreach (var token in StreamChatResponseAsync(character, conversationHistory, userMessage, cancellationToken))
        {
            response.Append(token);
        }
        return response.ToString();
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await InitializeAsync();
        
        if (_context == null || _model == null)
        {
            yield return "I apologize, but the on-device AI is not available. Please try again later.";
            yield break;
        }

        var prompt = BuildPrompt(character, conversationHistory, userMessage);
        
        var executor = new InteractiveExecutor(_context);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = 256,
            AntiPrompts = new[] { "User:", "Human:", "\n\n\n" },
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.7f,
                TopP = 0.9f
            }
        };

        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            yield return token;
        }
    }

    public async Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();
        
        if (_context == null || _model == null)
        {
            return GetFallbackPrayer(topic);
        }

        var prompt = $@"<|im_start|>system
You are a devoted Christian prayer writer. Write sincere, heartfelt prayers.
<|im_end|>
<|im_start|>user
Write a short, sincere prayer about: {topic}
<|im_end|>
<|im_start|>assistant
";

        var executor = new InteractiveExecutor(_context);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = 200,
            AntiPrompts = new[] { "<|im_end|>", "\n\n\n" },
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.8f,
                TopP = 0.9f
            }
        };

        var response = new StringBuilder();
        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            response.Append(token);
        }

        return response.ToString().Trim();
    }

    public async Task<string> GeneratePersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();
        
        if (_context == null || _model == null)
        {
            return GetFallbackPrayer(options.Topic);
        }

        // Build context hints
        var hints = new List<string>();
        if (options.Mood.HasValue)
            hints.Add($"The person is feeling {options.Mood.Value.ToString().ToLower()}");
        if (options.RequestType != PrayerRequestType.General)
            hints.Add($"Type: {options.RequestType.ToString().ToLower()} prayer");
        if (!string.IsNullOrEmpty(options.PrayingFor))
            hints.Add($"Praying for: {options.PrayingFor}");

        var contextHint = hints.Any() ? $"Context: {string.Join(". ", hints)}." : "";

        var prompt = $@"<|im_start|>system
You are a devoted Christian prayer writer. Write sincere, heartfelt prayers. {contextHint}
<|im_end|>
<|im_start|>user
Write a {options.Length.ToString().ToLower()}, sincere prayer about: {options.Topic}
<|im_end|>
<|im_start|>assistant
";

        var executor = new InteractiveExecutor(_context);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = options.Length.GetWordCount() * 2,
            AntiPrompts = new[] { "<|im_end|>", "\n\n\n" },
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.8f,
                TopP = 0.9f
            }
        };

        var response = new StringBuilder();
        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            response.Append(token);
        }

        return response.ToString().Trim();
    }

    public async Task<string> GenerateDevotionalAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        if (_context == null || _model == null)
        {
            return GetFallbackDevotional(date);
        }

        var prompt = $@"<|im_start|>system
You are a devotional writer. Generate a daily devotional as valid JSON with these exact fields:
{{""title"": ""title"", ""scripture"": ""verse"", ""scriptureReference"": ""reference"", ""content"": ""reflection"", ""prayer"": ""prayer"", ""category"": ""Faith/Hope/Love/Wisdom/Strength/Grace/Peace/Joy""}}
<|im_end|>
<|im_start|>user
Create a devotional for {date:MMMM d, yyyy}. Return only JSON.
<|im_end|>
<|im_start|>assistant
";

        var executor = new InteractiveExecutor(_context);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = 500,
            AntiPrompts = new[] { "<|im_end|>", "\n\n\n" },
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.7f,
                TopP = 0.9f
            }
        };

        var response = new StringBuilder();
        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            response.Append(token);
        }

        return response.ToString().Trim();
    }

    private static string GetFallbackDevotional(DateTime date)
    {
        return @"{
  ""title"": ""Walking in Faith Today"",
  ""scripture"": ""Trust in the LORD with all your heart and lean not on your own understanding; in all your ways submit to him, and he will make your paths straight."",
  ""scriptureReference"": ""Proverbs 3:5-6"",
  ""content"": ""Each day presents us with choices that require faith. When we trust in God's wisdom rather than relying solely on our own understanding, we open ourselves to His perfect guidance. Today, take a moment to surrender your plans and concerns to Him, knowing that He sees the bigger picture and will direct your steps."",
  ""prayer"": ""Lord, help me to trust You completely today. Guide my steps and give me wisdom in every decision I make. I surrender my plans to You. Amen."",
  ""category"": ""Faith""
}";
    }

    private string BuildPrompt(BiblicalCharacter character, List<ChatMessage> history, string userMessage)
    {
        var sb = new StringBuilder();
        
        // System prompt with character personality (kept short for small context)
        sb.AppendLine($"<|im_start|>system");
        sb.AppendLine($"You are {character.Name}, {character.Description}.");
        sb.AppendLine($"Speak as this biblical figure would - with wisdom, faith, and authenticity.");
        sb.AppendLine($"Keep responses brief but meaningful (2-4 sentences).");
        sb.AppendLine($"<|im_end|>");

        // Include only last 2-3 exchanges to fit in small context
        var recentHistory = history.TakeLast(6).ToList();
        foreach (var msg in recentHistory)
        {
            var role = msg.Role == "user" ? "user" : "assistant";
            sb.AppendLine($"<|im_start|>{role}");
            sb.AppendLine(msg.Content);
            sb.AppendLine($"<|im_end|>");
        }

        // Current user message
        sb.AppendLine($"<|im_start|>user");
        sb.AppendLine(userMessage);
        sb.AppendLine($"<|im_end|>");
        sb.AppendLine($"<|im_start|>assistant");

        return sb.ToString();
    }

    private static string GetFallbackPrayer(string topic)
    {
        return $@"Dear Heavenly Father,

We come before You today with hearts full of gratitude. We lift up our thoughts and concerns about {topic} to You, knowing that You hear every prayer.

Grant us Your wisdom, peace, and guidance. Help us to trust in Your perfect plan and timing.

In Jesus' name we pray,
Amen.";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _context?.Dispose();
        _model?.Dispose();
        _initLock.Dispose();
    }
}
