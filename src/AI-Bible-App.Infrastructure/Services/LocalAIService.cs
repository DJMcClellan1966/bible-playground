using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Implementation of AI service using local Phi-3.5-mini model via Ollama with RAG support.
/// Optimized for conversational performance with GPU acceleration and response caching.
/// </summary>
public class LocalAIService : IAIService
{
    private OllamaApiClient? _client;
    private readonly string _modelName;
    private readonly string _ollamaUrl;
    private readonly int _numCtx;
    private readonly int _numPredict;
    private readonly int _numGpu;
    private readonly int _numThread;
    private readonly ILogger<LocalAIService> _logger;
    private readonly IBibleRAGService? _ragService;
    private readonly bool _useRAG;
    
    // Optimized caching with thread-safe collections
    private readonly ConcurrentDictionary<string, CacheEntry<string>> _systemPromptCache = new();
    private readonly ConcurrentDictionary<string, CacheEntry<string>> _ragContextCache = new();
    private readonly ConcurrentDictionary<string, CacheEntry<string>> _conversationResponseCache = new();
    
    private readonly TimeSpan _systemPromptCacheExpiration;
    private readonly TimeSpan _ragContextCacheExpiration;
    private readonly int _maxRagCacheEntries;
    private readonly bool _conversationCacheEnabled;
    private readonly object _clientLock = new();
    
    // Cache entry with expiration
    private class CacheEntry<T>
    {
        public T Value { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public LocalAIService(
        IConfiguration configuration, 
        ILogger<LocalAIService> logger,
        IBibleRAGService? ragService = null)
    {
        _logger = logger;
        _ragService = ragService;
        
        _ollamaUrl = configuration["Ollama:Url"] ?? "http://localhost:11434";
        _modelName = configuration["Ollama:ModelName"] ?? "phi3.5:3.8b-mini-instruct-q4_K_M";
        _numCtx = int.TryParse(configuration["Ollama:NumCtx"], out var ctx) ? ctx : 4096;
        _numPredict = int.TryParse(configuration["Ollama:NumPredict"], out var pred) ? pred : 1024;
        _numGpu = int.TryParse(configuration["Ollama:NumGpu"], out var gpu) ? gpu : -1; // -1 = auto
        _numThread = int.TryParse(configuration["Ollama:NumThread"], out var thread) ? thread : 0; // 0 = auto
        _useRAG = configuration["RAG:Enabled"] == "true" || configuration["RAG:Enabled"] == null;
        
        // Caching configuration
        var systemPromptMinutes = int.TryParse(configuration["Caching:SystemPromptCacheMinutes"], out var spm) ? spm : 60;
        var ragMinutes = int.TryParse(configuration["Caching:RAGContextCacheMinutes"], out var rcm) ? rcm : 30;
        _systemPromptCacheExpiration = TimeSpan.FromMinutes(systemPromptMinutes);
        _ragContextCacheExpiration = TimeSpan.FromMinutes(ragMinutes);
        _maxRagCacheEntries = int.TryParse(configuration["Caching:MaxRAGCacheEntries"], out var mrc) ? mrc : 200;
        _conversationCacheEnabled = configuration["Caching:ConversationResponseCacheEnabled"] != "false";
        
        // DON'T create HttpClient/OllamaApiClient in constructor - causes WinUI3 crashes
        // Use lazy initialization instead
        
        _logger.LogInformation(
            "LocalAIService configured: Model={ModelName}, URL={Url}, RAG={RAGEnabled}, NumCtx={NumCtx}, NumPredict={NumPredict}, GPU={NumGpu}, Threads={NumThread}", 
            _modelName, 
            _ollamaUrl, 
            _useRAG && _ragService != null,
            _numCtx,
            _numPredict,
            _numGpu == -1 ? "auto" : _numGpu,
            _numThread == 0 ? "auto" : _numThread);
    }

    private OllamaApiClient GetClient()
    {
        if (_client == null)
        {
            lock (_clientLock)
            {
                if (_client == null)
                {
                    _logger.LogInformation("Lazy-initializing OllamaApiClient at {Url}...", _ollamaUrl);
                    var httpClient = new HttpClient
                    {
                        BaseAddress = new Uri(_ollamaUrl),
                        Timeout = TimeSpan.FromMinutes(5)
                    };
                    
                    _client = new OllamaApiClient(httpClient, _ollamaUrl)
                    {
                        SelectedModel = _modelName
                    };
                }
            }
        }
        return _client;
    }
    
    /// <summary>
    /// Get optimized request options with GPU acceleration
    /// </summary>
    private RequestOptions GetOptimizedRequestOptions(int? overrideNumPredict = null)
    {
        return new RequestOptions
        {
            NumCtx = _numCtx,
            NumPredict = overrideNumPredict ?? _numPredict,
            NumGpu = _numGpu,
            NumThread = _numThread > 0 ? _numThread : null
        };
    }

    public async Task<string> GetChatResponseAsync(
        BiblicalCharacter character, 
        List<Core.Models.ChatMessage> conversationHistory, 
        string userMessage, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<Message>
            {
                new Message
                {
                    Role = ChatRole.System,
                    Content = character.SystemPrompt
                }
            };

            // RAG: Retrieve relevant Bible verses if enabled
            string? retrievedContext = null;
            if (_useRAG && _ragService != null && _ragService.IsInitialized)
            {
                retrievedContext = await GetRelevantScriptureContextAsync(userMessage, cancellationToken);
                
                if (!string.IsNullOrEmpty(retrievedContext))
                {
                    // Add retrieved verses as context
                    messages.Add(new Message
                    {
                        Role = ChatRole.System,
                        Content = $"Relevant Scripture passages for context:\n{retrievedContext}\n\nUse these passages to inform your response when appropriate."
                    });
                    
                    _logger.LogDebug("Added RAG context to chat request");
                }
            }

            // Add conversation history (limit to last 12 messages for roundtable context)
            foreach (var msg in conversationHistory.TakeLast(12))
            {
                messages.Add(new Message
                {
                    Role = msg.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                    Content = msg.Content
                });
            }

            // Add current user message
            messages.Add(new Message
            {
                Role = ChatRole.User,
                Content = userMessage
            });

            var request = new ChatRequest
            {
                Model = _modelName,
                Messages = messages,
                Options = GetOptimizedRequestOptions()
            };

            _logger.LogDebug("Sending chat request to Ollama with {MessageCount} messages", messages.Count);

            var responseText = string.Empty;
            
            await foreach (var response in GetClient().ChatAsync(request, cancellationToken))
            {
                if (response?.Message?.Content != null)
                {
                    responseText += response.Message.Content;
                }
            }
            
            if (string.IsNullOrEmpty(responseText))
            {
                throw new InvalidOperationException("Received null or empty response from Ollama");
            }

            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response from local AI model");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        BiblicalCharacter character, 
        List<Core.Models.ChatMessage> conversationHistory, 
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<Message>
        {
            new Message
            {
                Role = ChatRole.System,
                Content = GetCachedSystemPrompt(character)
            }
        };

        // RAG: Retrieve relevant Bible verses if enabled (with caching)
        if (_useRAG && _ragService != null && _ragService.IsInitialized)
        {
            var retrievedContext = await GetCachedRelevantScriptureContextAsync(userMessage, cancellationToken);
            
            if (!string.IsNullOrEmpty(retrievedContext))
            {
                messages.Add(new Message
                {
                    Role = ChatRole.System,
                    Content = $"Relevant Scripture passages for context:\n{retrievedContext}\n\nUse these passages to inform your response when appropriate."
                });
            }
        }

        // Add conversation history (limit to last 6 messages for speed)
        foreach (var msg in conversationHistory.TakeLast(6))
        {
            messages.Add(new Message
            {
                Role = msg.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                Content = msg.Content
            });
        }

        // Add current user message
        messages.Add(new Message
        {
            Role = ChatRole.User,
            Content = userMessage
        });

        var request = new ChatRequest
        {
            Model = _modelName,
            Messages = messages,
            Options = GetOptimizedRequestOptions()
        };

        _logger.LogDebug("Streaming chat response with {MessageCount} messages", messages.Count);

        IAsyncEnumerable<ChatResponseStream?> responseStream;
        string? initError = null;
        try
        {
            responseStream = GetClient().ChatAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start chat stream");
            initError = ex.Message;
            responseStream = null!;
        }

        if (initError != null)
        {
            yield return $"__ERROR__:{initError}";
            yield break;
        }

        await foreach (var response in responseStream)
        {
            if (response?.Message?.Content != null)
            {
                yield return response.Message.Content;
            }
        }
    }

    public async Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default)
    {
        try
        {
            var systemPrompt = @"You are a compassionate prayer writer. Generate heartfelt, biblical prayers that are meaningful and spiritually uplifting.

IMPORTANT GUIDELINES:
- Keep prayers concise (2-3 short paragraphs)
- Use natural, sincere language that flows like a real conversation with God
- Do NOT include parenthetical references like (Psalm 23:1) or (Matthew 6:9) in the prayer
- Do NOT include any meta-commentary, instructions, or notes
- Do NOT use overly flowery or verbose language
- Let scripture themes inspire the prayer naturally without citing chapter and verse
- Write the prayer directly - start with addressing God and end with Amen
- The output should ONLY be the prayer text itself, nothing else";

            var userPrompt = string.IsNullOrEmpty(topic) 
                ? "Generate a daily prayer for guidance, strength, and gratitude." 
                : $"Generate a prayer about: {topic}";

            var messages = new List<Message>
            {
                new Message
                {
                    Role = ChatRole.System,
                    Content = systemPrompt
                }
            };

            // RAG: Retrieve relevant Bible verses for prayer context
            if (_useRAG && _ragService != null && _ragService.IsInitialized)
            {
                var retrievedContext = await GetRelevantScriptureContextAsync(
                    topic ?? "daily prayer guidance strength gratitude", 
                    cancellationToken);
                
                if (!string.IsNullOrEmpty(retrievedContext))
                {
                    messages.Add(new Message
                    {
                        Role = ChatRole.System,
                        Content = $"Relevant Scripture passages to inspire the prayer:\n{retrievedContext}"
                    });
                    
                    _logger.LogDebug("Added RAG context to prayer generation");
                }
            }

            messages.Add(new Message
            {
                Role = ChatRole.User,
                Content = userPrompt
            });

            var request = new ChatRequest
            {
                Model = _modelName,
                Messages = messages
            };

            _logger.LogDebug("Generating prayer with topic: {Topic}", topic ?? "general");

            var responseText = string.Empty;
            
            await foreach (var response in GetClient().ChatAsync(request, cancellationToken))
            {
                if (response?.Message?.Content != null)
                {
                    responseText += response.Message.Content;
                }
            }
            
            if (string.IsNullOrEmpty(responseText))
            {
                throw new InvalidOperationException("Received null or empty response from Ollama");
            }

            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating prayer from local AI model");
            throw;
        }
    }

    /// <summary>
    /// Retrieve relevant Scripture context using RAG
    /// </summary>
    private async Task<string?> GetRelevantScriptureContextAsync(
        string query, 
        CancellationToken cancellationToken)
    {
        try
        {
            if (_ragService == null || !_ragService.IsInitialized)
            {
                return null;
            }

            var relevantChunks = await _ragService.RetrieveRelevantVersesAsync(
                query, 
                limit: 3,
                minRelevanceScore: 0.6,
                cancellationToken: cancellationToken);

            if (!relevantChunks.Any())
            {
                _logger.LogDebug("No relevant Scripture found for query: {Query}", query);
                return null;
            }

            var context = new StringBuilder();
            foreach (var chunk in relevantChunks)
            {
                context.AppendLine($"{chunk.Reference}:");
                context.AppendLine(chunk.Text);
                context.AppendLine();
            }

            _logger.LogInformation(
                "Retrieved {Count} relevant Scripture passages for query", 
                relevantChunks.Count);

            return context.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Scripture context");
            return null;
        }
    }

    /// <summary>
    /// Get cached system prompt for character (thread-safe with expiration)
    /// </summary>
    private string GetCachedSystemPrompt(BiblicalCharacter character)
    {
        var cacheKey = $"prompt_{character.Id}";
        
        if (_systemPromptCache.TryGetValue(cacheKey, out var entry) && !entry.IsExpired)
        {
            return entry.Value;
        }
        
        var newEntry = new CacheEntry<string>
        {
            Value = character.SystemPrompt,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_systemPromptCacheExpiration)
        };
        
        _systemPromptCache[cacheKey] = newEntry;
        _logger.LogDebug("Cached system prompt for {Character} (expires in {Minutes} min)", 
            character.Name, _systemPromptCacheExpiration.TotalMinutes);
        
        return newEntry.Value;
    }

    /// <summary>
    /// Get cached RAG context (thread-safe with expiration and LRU cleanup)
    /// </summary>
    private async Task<string?> GetCachedRelevantScriptureContextAsync(
        string query, 
        CancellationToken cancellationToken)
    {
        var cacheKey = $"rag_{query.ToLowerInvariant().GetHashCode()}";
        
        // Check cache first
        if (_ragContextCache.TryGetValue(cacheKey, out var entry) && !entry.IsExpired)
        {
            _logger.LogDebug("Using cached RAG context for query");
            return entry.Value;
        }

        var context = await GetRelevantScriptureContextAsync(query, cancellationToken);
        
        if (!string.IsNullOrEmpty(context))
        {
            var newEntry = new CacheEntry<string>
            {
                Value = context,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_ragContextCacheExpiration)
            };
            
            _ragContextCache[cacheKey] = newEntry;
            _logger.LogDebug("Cached RAG context for query (expires in {Minutes} min)", 
                _ragContextCacheExpiration.TotalMinutes);
            
            // Clean up expired/excess entries (LRU-style)
            CleanupRagCache();
        }
        
        return context;
    }
    
    /// <summary>
    /// Remove expired entries and enforce max cache size
    /// </summary>
    private void CleanupRagCache()
    {
        // Remove expired entries
        var expiredKeys = _ragContextCache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredKeys)
        {
            _ragContextCache.TryRemove(key, out _);
        }
        
        // Enforce max size (remove oldest entries)
        if (_ragContextCache.Count > _maxRagCacheEntries)
        {
            var entriesToRemove = _ragContextCache
                .OrderBy(kvp => kvp.Value.CreatedAt)
                .Take(_ragContextCache.Count - _maxRagCacheEntries)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in entriesToRemove)
            {
                _ragContextCache.TryRemove(key, out _);
            }
            
            _logger.LogDebug("Cleaned up {Count} old RAG cache entries", entriesToRemove.Count);
        }
    }
    
    /// <summary>
    /// Generate a cache key for conversation context (for response caching)
    /// </summary>
    private string GetConversationCacheKey(string characterId, List<Core.Models.ChatMessage> history, string userMessage)
    {
        // Hash based on character, recent history summary, and message
        var historyHash = history.TakeLast(3)
            .Aggregate(0, (h, m) => h ^ m.Content.GetHashCode());
        return $"conv_{characterId}_{historyHash}_{userMessage.ToLowerInvariant().GetHashCode()}";
    }
}
