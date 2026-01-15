using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Core.Services;
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
    private readonly IKnowledgeBaseService? _knowledgeBaseService;
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
        IBibleRAGService? ragService = null,
        IKnowledgeBaseService? knowledgeBaseService = null)
    {
        _logger = logger;
        _ragService = ragService;
        _knowledgeBaseService = knowledgeBaseService;
        
        _ollamaUrl = configuration["Ollama:Url"] ?? "http://localhost:11434";
        _modelName = configuration["Ollama:ModelName"] ?? "phi3:mini";
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
                    try
                    {
                        _logger.LogInformation("Initializing OllamaApiClient at {Url} with model {Model}...", _ollamaUrl, _modelName);
                        
                        // Test connection first with a simple HttpClient
                        try
                        {
                            using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                            var testUrl = $"{_ollamaUrl}/api/tags";
                            _logger.LogInformation("Testing connection to {TestUrl}...", testUrl);
                            
                            // Also log to file for debugging
                            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIBibleApp", "ollama-debug.log");
                            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                            File.AppendAllText(logPath, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Testing {testUrl}...\n");
                            
                            var testResponse = testClient.GetAsync(testUrl).GetAwaiter().GetResult();
                            _logger.LogInformation("Connection test result: {StatusCode}", testResponse.StatusCode);
                            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Success: {testResponse.StatusCode}\n");
                            
                            if (!testResponse.IsSuccessStatusCode)
                            {
                                throw new HttpRequestException($"Ollama returned status {testResponse.StatusCode}");
                            }
                        }
                        catch (Exception testEx)
                        {
                            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIBibleApp", "ollama-debug.log");
                            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {testEx.GetType().Name} - {testEx.Message}\n");
                            if (testEx.InnerException != null)
                            {
                                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inner: {testEx.InnerException.Message}\n");
                            }
                            
                            _logger.LogError(testEx, "Connection test failed to {Url}", _ollamaUrl);
                            throw new InvalidOperationException(
                                $"Cannot reach Ollama at {_ollamaUrl}. " +
                                $"Error: {testEx.Message}. " +
                                $"Check the log file at: {logPath}. " +
                                $"Ensure Ollama is running with: ollama serve", 
                                testEx);
                        }
                        
                        var httpClient = new HttpClient
                        {
                            BaseAddress = new Uri(_ollamaUrl),
                            Timeout = TimeSpan.FromMinutes(5)
                        };
                        
                        _client = new OllamaApiClient(httpClient, _ollamaUrl)
                        {
                            SelectedModel = _modelName
                        };
                        
                        _logger.LogInformation("OllamaApiClient initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to initialize OllamaApiClient. URL: {Url}, Model: {Model}", _ollamaUrl, _modelName);
                        throw new InvalidOperationException($"Cannot connect to Ollama at {_ollamaUrl}. Ensure Ollama is running with: ollama serve", ex);
                    }
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
        var maxPredict = overrideNumPredict ?? _numPredict;
        var maxCtx = _numCtx;
        var temperature = 0.8f;
        var topP = 0.95f;
        var repeatPenalty = 1.15f;

        if (PerformanceSettings.IsEnabled)
        {
            maxPredict = Math.Min(maxPredict, 512);
            maxCtx = Math.Min(maxCtx, 2048);
            temperature = 0.6f;
            topP = 0.9f;
            repeatPenalty = 1.2f;
        }

        return new RequestOptions
        {
            NumCtx = maxCtx,
            NumPredict = maxPredict,
            NumGpu = _numGpu,
            NumThread = _numThread > 0 ? _numThread : null,
            Temperature = temperature,
            TopP = topP,
            RepeatPenalty = repeatPenalty
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
                },
                new Message
                {
                    Role = ChatRole.System,
                    Content = @"CRITICAL RESPONSE RULES - FOLLOW EXACTLY:
1. READ THE USER'S ACTUAL QUESTION - Don't ignore it and give a generic response
2. Your response must DIRECTLY ADDRESS what they asked - if they ask about fear, talk about fear; if they ask about relationships, talk about relationships
3. Connect your biblical experience to THEIR SPECIFIC situation - use examples that actually relate to their question
4. DO NOT give the same response you gave before - vary your stories and experiences
5. Keep responses SHORT (2-3 paragraphs max) and CONVERSATIONAL
6. If you don't understand their question, ASK for clarification instead of giving a generic answer

Remember: You are having a CONVERSATION, not giving a prepared speech. Listen to them!"
                }
            };

            // RAG: Retrieve relevant Bible verses if enabled
            string? retrievedContext = null;
            if (!PerformanceSettings.IsEnabled &&
                _useRAG && _ragService != null && _ragService.IsInitialized)
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

            // Knowledge Base: Add historical/cultural context and language insights
            if (!PerformanceSettings.IsEnabled && _knowledgeBaseService != null)
            {
                // Get historical context
                var historicalContexts = await _knowledgeBaseService.GetHistoricalContextAsync(
                    character.Id, 
                    userMessage, 
                    maxResults: 2);
                    
                if (historicalContexts.Any())
                {
                    var contextText = string.Join("\n\n", historicalContexts.Select(c => 
                        $"[Historical Context: {c.Title}]\n{c.Content}"));
                    
                    messages.Add(new Message
                    {
                        Role = ChatRole.System,
                        Content = $"Historical & Cultural Context for your time period:\n{contextText}\n\nUse this context to add depth and authenticity to your response."
                    });
                    
                    _logger.LogDebug("Added {Count} historical contexts", historicalContexts.Count);
                }
                
                // Find thematic connections if user references a passage
                if (userMessage.Contains("Genesis") || userMessage.Contains("Exodus") || 
                    userMessage.Contains("Matthew") || userMessage.Contains("John") ||
                    userMessage.Contains("Romans") || userMessage.Contains("Corinthians"))
                {
                    var connections = await _knowledgeBaseService.FindThematicConnectionsAsync(
                        userMessage, 
                        "", 
                        maxResults: 2);
                        
                    if (connections.Any())
                    {
                        var connectionText = string.Join("\n\n", connections.Select(c => 
                            $"[Connection: {c.Theme}]\n{c.PrimaryPassage} ↔ {c.SecondaryPassage}\n{c.Insight}"));
                        
                        messages.Add(new Message
                        {
                            Role = ChatRole.System,
                            Content = $"Thematic Connections you might explore:\n{connectionText}\n\nConsider mentioning these connections if relevant to the conversation."
                        });
                        
                        _logger.LogDebug("Added {Count} thematic connections", connections.Count);
                    }
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

            // Add current user message with emphasis
            messages.Add(new Message
            {
                Role = ChatRole.User,
                Content = $"{userMessage}\n\n[Remember: Respond specifically to THIS question, not a generic response]"
            });

            var request = new ChatRequest
            {
                Model = _modelName,
                Messages = messages,
                Options = GetOptimizedRequestOptions()
            };

            _logger.LogDebug("Sending chat request to Ollama with {MessageCount} messages", messages.Count);

            var responseText = string.Empty;
            
            try
            {
                await foreach (var response in GetClient().ChatAsync(request, cancellationToken))
                {
                    if (response?.Message?.Content != null)
                    {
                        responseText += response.Message.Content;
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP connection error to Ollama at {Url}", _ollamaUrl);
                throw new InvalidOperationException($"Cannot connect to Ollama at {_ollamaUrl}. Ensure Ollama is running with: ollama serve", httpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Ollama chat streaming");
                throw;
            }
            
            if (string.IsNullOrEmpty(responseText))
            {
                throw new InvalidOperationException("Received null or empty response from Ollama");
            }

            return responseText;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw our custom exceptions as-is
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
            },
            new Message
            {
                Role = ChatRole.System,
                Content = @"CRITICAL RESPONSE RULES - FOLLOW EXACTLY:
1. READ THE USER'S ACTUAL QUESTION - Don't ignore it and give a generic response
2. Your response must DIRECTLY ADDRESS what they asked - if they ask about fear, talk about fear; if they ask about relationships, talk about relationships
3. Connect your biblical experience to THEIR SPECIFIC situation - use examples that actually relate to their question
4. DO NOT give the same response you gave before - vary your stories and experiences
5. Keep responses SHORT (2-3 paragraphs max) and CONVERSATIONAL
6. If you don't understand their question, ASK for clarification instead of giving a generic answer

Remember: You are having a CONVERSATION, not giving a prepared speech. Listen to them!"
            }
        };

        // RAG: Retrieve relevant Bible verses if enabled (with caching)
        if (!PerformanceSettings.IsEnabled &&
            _useRAG && _ragService != null && _ragService.IsInitialized)
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

        // Add current user message with emphasis
        messages.Add(new Message
        {
            Role = ChatRole.User,
            Content = $"{userMessage}\n\n[Remember: Respond specifically to THIS question, not a generic response]"
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
                Messages = messages,
                Options = GetOptimizedRequestOptions(256) // Short prayers need fewer tokens
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

    public async Task<string> GeneratePersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            // Build personalized prompt based on options
            var promptParts = new List<string>();
            
            // Style/type instruction
            if (options.RequestType != PrayerRequestType.General)
            {
                promptParts.Add($"Write {options.RequestType.GetDescription()}.");
            }
            
            // Tradition style hint
            promptParts.Add(options.Tradition.GetStyleHint());
            
            // Mood context
            if (options.Mood.HasValue)
            {
                promptParts.Add($"The person praying is {options.Mood.Value.GetDescription()}.");
            }
            
            // Time context
            if (options.TimeContext.HasValue)
            {
                promptParts.Add($"This prayer is {options.TimeContext.Value.GetTimeGreeting()}.");
            }
            
            // Life circumstances
            if (!string.IsNullOrEmpty(options.LifeCircumstances))
            {
                promptParts.Add($"Life context: {options.LifeCircumstances}");
            }
            
            // Prayer intentions
            if (options.Intentions.Any())
            {
                promptParts.Add($"Include these intentions: {string.Join(", ", options.Intentions)}");
            }
            
            // Praying for someone specific
            if (!string.IsNullOrEmpty(options.PrayingFor))
            {
                promptParts.Add($"This prayer is for/about: {options.PrayingFor}");
            }
            
            // Scripture preference
            if (options.IncludeScripture)
            {
                promptParts.Add("Let Scripture themes inspire the prayer naturally, but don't cite chapter and verse.");
            }
            else
            {
                promptParts.Add("Do not include Scripture references.");
            }
            
            // Length guidance
            var wordCount = options.Length.GetWordCount();
            promptParts.Add($"Keep the prayer approximately {wordCount} words.");

            var systemPrompt = $@"You are a compassionate prayer writer. Generate heartfelt, biblically-grounded prayers.

{string.Join("\n", promptParts)}

IMPORTANT GUIDELINES:
- Write the prayer directly - start with addressing God and end with Amen
- Use natural, sincere language that flows like a real conversation with God
- Do NOT include parenthetical references or meta-commentary
- The output should ONLY be the prayer text itself";

            var userPrompt = string.IsNullOrEmpty(options.Topic) 
                ? "Generate a personalized prayer." 
                : $"Generate a prayer about: {options.Topic}";

            var messages = new List<Message>
            {
                new Message { Role = ChatRole.System, Content = systemPrompt }
            };

            // RAG: Retrieve relevant Bible verses for prayer context
            if (_useRAG && _ragService != null && _ragService.IsInitialized && options.IncludeScripture)
            {
                var searchQuery = options.Topic;
                if (options.Mood.HasValue)
                    searchQuery += " " + options.Mood.Value.ToString().ToLower();
                if (options.RequestType != PrayerRequestType.General)
                    searchQuery += " " + options.RequestType.ToString().ToLower();
                    
                var retrievedContext = await GetRelevantScriptureContextAsync(searchQuery, cancellationToken);
                
                if (!string.IsNullOrEmpty(retrievedContext))
                {
                    messages.Add(new Message
                    {
                        Role = ChatRole.System,
                        Content = $"Relevant Scripture passages to inspire the prayer:\n{retrievedContext}"
                    });
                }
            }

            messages.Add(new Message { Role = ChatRole.User, Content = userPrompt });

            var request = new ChatRequest
            {
                Model = _modelName,
                Messages = messages,
                Options = GetOptimizedRequestOptions(256) // Limit tokens for faster prayer generation
            };

            _logger.LogDebug("Generating personalized prayer with style: {Style}, mood: {Mood}", options.RequestType, options.Mood);

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
            _logger.LogError(ex, "Error generating personalized prayer from local AI model");
            throw;
        }
    }

    public async Task<string> GenerateDevotionalAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            var systemPrompt = @"You are a devotional writer creating daily spiritual content. Generate a complete devotional with all required sections.

CRITICAL: Your response MUST be valid JSON with these exact fields:
{
  ""title"": ""A meaningful title (5-8 words)"",
  ""scripture"": ""The full Bible verse text"",
  ""scriptureReference"": ""Book Chapter:Verse"",
  ""content"": ""A thoughtful reflection (150-200 words)"",
  ""prayer"": ""A heartfelt prayer (50-75 words)"",
  ""category"": ""One of: Faith, Hope, Love, Wisdom, Strength, Grace, Peace, Joy""
}

IMPORTANT:
- Output ONLY the JSON object, no markdown code blocks
- Make the content inspiring and practical for daily life
- The prayer should be sincere and personal";

            var userPrompt = $"Create a daily devotional for {date:MMMM d, yyyy}. Return only valid JSON.";

            var messages = new List<Message>
            {
                new Message { Role = ChatRole.System, Content = systemPrompt },
                new Message { Role = ChatRole.User, Content = userPrompt }
            };

            var request = new ChatRequest
            {
                Model = _modelName,
                Messages = messages,
                Options = GetOptimizedRequestOptions(384) // Devotionals need a bit more tokens
            };

            _logger.LogDebug("Generating devotional for date: {Date}", date);

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
            _logger.LogError(ex, "Error generating devotional from local AI model");
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
