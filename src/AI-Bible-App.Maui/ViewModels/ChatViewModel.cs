using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Core.Services;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Maui.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Globalization;

#pragma warning disable MVVMTK0045 // AOT compatibility warning for WinRT scenarios

namespace AI_Bible_App.Maui.ViewModels;

public partial class ChatViewModel : BaseViewModel, IDisposable
{
    private readonly IAIService _aiService;
    private readonly IChatRepository _chatRepository;
    private readonly IBibleLookupService _bibleLookupService;
    private readonly IReflectionRepository _reflectionRepository;
    private readonly IPrayerRepository _prayerRepository;
    private readonly IDialogService _dialogService;
    private readonly IContentModerationService _moderationService;
    private readonly IUserService _userService;
    private readonly ICharacterVoiceService _voiceService;
    private readonly CharacterIntelligenceService? _intelligenceService;
    private readonly PersonalizedPromptService? _personalizedPromptService;
    private readonly UserProgressionService? _progressionService;
    private readonly IUserQuestionCollector? _questionCollector;
    private readonly IConversationQuotaService _quotaService;
    private readonly bool _enableContextualReferences;
    private readonly IHealthCheckService? _healthCheckService;
    private readonly IUsageMetricsService? _usageMetrics;
    private ChatSession? _currentSession;
    private CancellationTokenSource? _speechCancellationTokenSource;
    private CancellationTokenSource? _aiResponseCancellationTokenSource;
    private CancellationTokenSource? _voiceCancellationTokenSource;
    private ChatMessage? _currentlySpeakingMessage;
    private BiblicalCharacter? _personalizedCharacter;

    // Event to request scroll to bottom
    public event EventHandler? ScrollToBottomRequested;

    [ObservableProperty]
    private BiblicalCharacter? character;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> messages = new();

    [ObservableProperty]
    private string userMessage = string.Empty;

    [ObservableProperty]
    private bool isAiTyping;

    [ObservableProperty]
    private bool isListening;

    [ObservableProperty]
    private bool isSpeaking;

    [ObservableProperty]
    private string? speakingMessageId;

    [ObservableProperty]
    private bool isGeneratingPrayer;

    [ObservableProperty]
    private ChatMessage? latestAssistantMessage;

    [ObservableProperty]
    private bool isSavingReflection;

    [ObservableProperty]
    private bool isActionInProgress;

    [ObservableProperty]
    private bool isModelWarningVisible;

    [ObservableProperty]
    private string modelWarningText = string.Empty;

    public ChatViewModel(IAIService aiService, IChatRepository chatRepository, IBibleLookupService bibleLookupService, IReflectionRepository reflectionRepository, IPrayerRepository prayerRepository, IDialogService dialogService, IContentModerationService moderationService, IUserService userService, ICharacterVoiceService voiceService, IConfiguration configuration, IConversationQuotaService quotaService, CharacterIntelligenceService? intelligenceService = null, PersonalizedPromptService? personalizedPromptService = null, IUserQuestionCollector? questionCollector = null, UserProgressionService? progressionService = null, IHealthCheckService? healthCheckService = null, IUsageMetricsService? usageMetrics = null)
    {
        _aiService = aiService;
        _chatRepository = chatRepository;
        _bibleLookupService = bibleLookupService;
        _reflectionRepository = reflectionRepository;
        _prayerRepository = prayerRepository;
        _dialogService = dialogService;
        _moderationService = moderationService;
        _userService = userService;
        _voiceService = voiceService;
        _intelligenceService = intelligenceService;
        _personalizedPromptService = personalizedPromptService;
        _progressionService = progressionService;
        _questionCollector = questionCollector;
        _quotaService = quotaService;
        var defaultContextual = configuration["Features:ContextualReferences"]?.ToLower() == "true";
        _enableContextualReferences = Preferences.Get("contextual_refs_enabled", defaultContextual);
        _healthCheckService = healthCheckService;
        _usageMetrics = usageMetrics;
    }

    public async Task InitializeAsync(BiblicalCharacter character, ChatSession? existingSession = null, bool forceNewChat = false)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] ChatViewModel.InitializeAsync START - character: {character.Name}, forceNewChat: {forceNewChat}");
            
            Character = character;
            Title = $"Chat with {character.Name}";
            
            // Get personalized character with user context injected into system prompt
            var currentUserId = _userService.CurrentUser?.Id ?? "default";
            if (_personalizedPromptService != null)
            {
                _personalizedCharacter = await _personalizedPromptService.GetPersonalizedCharacterAsync(character, currentUserId);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Personalized character loaded for user {currentUserId}");
            }
            else
            {
                _personalizedCharacter = character;
            }
            
            // If forcing new chat, don't look for existing session
            if (!forceNewChat && existingSession == null)
            {
                existingSession = await _chatRepository.GetLatestSessionForCharacterAsync(character.Id);
            }
            
            if (!forceNewChat && existingSession != null)
            {
                // Resume existing session and update timestamp
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Resuming existing session with {existingSession.Messages.Count} messages");
                _currentSession = existingSession;
                _currentSession.StartedAt = DateTime.UtcNow; // Update to show it's active
                Messages = new ObservableCollection<ChatMessage>(existingSession.Messages);
                
                // Set latest assistant message from history
                LatestAssistantMessage = Messages.LastOrDefault(m => m.Role == "assistant");
            }
            else
            {
                // Start new session
                System.Diagnostics.Debug.WriteLine("[DEBUG] Creating new chat session");
                _currentSession = new ChatSession
                {
                    Id = Guid.NewGuid().ToString(),
                    CharacterId = character.Id,
                    StartedAt = DateTime.UtcNow,
                    Messages = new List<ChatMessage>()
                };

                var welcomeMessage = new ChatMessage
                {
                    Role = "assistant",
                    Content = $"Peace be with you! I am {character.Name}, {character.Description}. How may I help you today?",
                    Timestamp = DateTime.UtcNow
                };
                
                Messages.Add(welcomeMessage);
                _currentSession.Messages.Add(welcomeMessage);
                LatestAssistantMessage = welcomeMessage;
            }
            
            await CheckModelHealthAsync();
            System.Diagnostics.Debug.WriteLine($"[DEBUG] ChatViewModel.InitializeAsync END - messages: {Messages.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] ChatViewModel.InitializeAsync FAILED: {ex}");
            throw;
        }
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void ToggleReferences(ChatMessage? message)
    {
        if (message == null)
            return;

        message.IsReferencesExpanded = !message.IsReferencesExpanded;
    }

    private async Task CheckModelHealthAsync()
    {
        if (_healthCheckService == null)
            return;

        try
        {
            var status = await _healthCheckService.GetHealthStatusAsync();
            if (!status.IsHealthy)
            {
                IsModelWarningVisible = true;
                ModelWarningText = status.ErrorMessage ?? UserFriendlyErrors.OllamaConnectionError;
            }
        }
        catch (Exception ex)
        {
            IsModelWarningVisible = true;
            ModelWarningText = $"Model health check failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleSpeechToText()
    {
        if (IsListening)
        {
            // Stop listening
            _speechCancellationTokenSource?.Cancel();
            IsListening = false;
            return;
        }

        try
        {
            // Check and request microphone permission
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted)
                {
                    await _dialogService.ShowAlertAsync(
                        "Permission Required",
                        "Microphone permission is needed for speech-to-text.");
                    return;
                }
            }

            IsListening = true;
            _speechCancellationTokenSource = new CancellationTokenSource();

            var recognitionResult = await SpeechToText.Default.ListenAsync(
                CultureInfo.CurrentCulture,
                new Progress<string>(partialText =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UserMessage = partialText;
                    });
                }),
                _speechCancellationTokenSource.Token);

            if (recognitionResult.IsSuccessful)
            {
                UserMessage = recognitionResult.Text;
            }
            else if (!string.IsNullOrEmpty(recognitionResult.Text))
            {
                UserMessage = recognitionResult.Text;
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled - this is fine
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Speech recognition error: {ex.Message}");
            await _dialogService.ShowAlertAsync(
                "Speech Error",
                $"Could not recognize speech: {ex.Message}");
        }
        finally
        {
            IsListening = false;
            _speechCancellationTokenSource?.Dispose();
            _speechCancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] SendMessage called. UserMessage: \"{UserMessage}\", Character: {Character?.Name}, IsAiTyping: {IsAiTyping}");
        
        if (string.IsNullOrWhiteSpace(UserMessage) || Character == null || IsAiTyping)
        {
            System.Diagnostics.Debug.WriteLine("[DEBUG] SendMessage early exit");
            return;
        }

        try
        {
            ClearModelWarning();
            // Check conversation quota before proceeding
            var currentUserId = _userService.CurrentUser?.Id ?? "default";
            if (!await _quotaService.CanSendMessageAsync(currentUserId))
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Quota exceeded - showing upgrade dialog");
                var quotaInfo = await _quotaService.GetDailyQuotaAsync(currentUserId);
                
                var result = await _dialogService.ShowConfirmAsync(
                    "Daily Limit Reached",
                    $"You've reached your daily limit of {quotaInfo.MessagesLimit} conversations.\n\n" +
                    $"Upgrade to Premium for unlimited conversations, advanced AI models, and more!\n\n" +
                    $"Quota resets at: {quotaInfo.ResetTime:hh:mm tt}",
                    "Upgrade to Premium",
                    "Maybe Later");
                
                if (result)
                {
                    // Navigate to subscription page
                    await Shell.Current.GoToAsync("//SubscriptionPage");
                }
                
                return;
            }
            
            // Content moderation check (if enabled for user)
            var shouldModerate = _userService.CurrentUser?.Settings.EnableContentModeration ?? true;
            if (shouldModerate)
            {
                var moderationResult = _moderationService.CheckContent(UserMessage);
                if (!moderationResult.IsAppropriate)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Message blocked by moderation: {moderationResult.Reason}");
                    await _dialogService.ShowAlertAsync(
                        "Message Not Sent",
                        moderationResult.Reason ?? "Please rephrase your message with appropriate language.");
                    return;
                }
            }

            System.Diagnostics.Debug.WriteLine("[DEBUG] Setting IsAiTyping = true");
            IsAiTyping = true;
            _aiResponseCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _aiResponseCancellationTokenSource.Token;
            
            var userMsg = UserMessage;
            UserMessage = string.Empty;

            var chatMessage = new ChatMessage
            {
                Role = "user",
                Content = userMsg,
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(chatMessage);
            _currentSession?.Messages.Add(chatMessage);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Added user message: {userMsg}");

            _usageMetrics?.TrackCharacterConversation(Character.Id);
            
            // Record message sent for quota tracking
            await _quotaService.RecordMessageSentAsync(currentUserId);
            System.Diagnostics.Debug.WriteLine("[DEBUG] Recorded message for quota tracking");
            
            // Collect user question for training data (with consent check)
            if (_questionCollector != null && _userService.CurrentUser != null)
            {
                var userConsented = _userService.CurrentUser.Settings?.ShareDataForImprovement ?? false;
                await _questionCollector.SaveUserQuestionAsync(userMsg, Character.Id, userConsented);
            }
            
            // 
            // Request scroll to bottom after user message
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);

            // Create AI message placeholder for streaming
            var aiMessage = new ChatMessage
            {
                Role = "assistant",
                Content = "",
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(aiMessage);

            System.Diagnostics.Debug.WriteLine("[DEBUG] Starting streaming response...");
            var conversationHistory = Messages.Where(m => m != aiMessage).ToList();
            
            // Use personalized character (with user context) if available
            var characterForAI = _personalizedCharacter ?? Character;

            if (_intelligenceService != null)
            {
                try
                {
                    var synthesizedPrompt = await _intelligenceService.SynthesizePromptAsync(
                        characterForAI,
                        characterForAI.SystemPrompt,
                        userMsg);
                    characterForAI = CloneCharacterWithPrompt(characterForAI, synthesizedPrompt);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Intelligence synth error: {ex.Message}");
                }
            }
            
            // Stream the response on background thread, update UI on main thread
            // Use ConfigureAwait(false) to not block UI thread while waiting for tokens
            try
            {
                var tokenBuffer = new System.Text.StringBuilder();
                var lastUpdateTime = DateTime.UtcNow;
                
                await foreach (var token in _aiService.StreamChatResponseAsync(characterForAI, conversationHistory, userMsg, cancellationToken).ConfigureAwait(false))
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    tokenBuffer.Append(token);
                    
                    // Batch UI updates for better responsiveness (every 50ms or 5+ tokens)
                    var now = DateTime.UtcNow;
                    if ((now - lastUpdateTime).TotalMilliseconds >= 50 || tokenBuffer.Length >= 5)
                    {
                        var bufferedContent = tokenBuffer.ToString();
                        tokenBuffer.Clear();
                        lastUpdateTime = now;
                        
                        // Update UI on main thread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            aiMessage.Content += bufferedContent;
                        });
                    }
                }
                
                // Flush any remaining tokens
                if (tokenBuffer.Length > 0)
                {
                    var remaining = tokenBuffer.ToString();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        aiMessage.Content += remaining;
                    });
                }
            }
            catch (Exception streamEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Streaming ERROR: {streamEx}");
                var userFriendlyError = UserFriendlyErrors.GetUserFriendlyMessage(streamEx);
                ShowModelWarningIfRelevant(userFriendlyError);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    aiMessage.Content += $"\n\n⚠️ {userFriendlyError}";
                });
            }
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Streaming complete. Response length: {aiMessage.Content?.Length ?? 0}");
            
            // Update latest assistant message for action buttons
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LatestAssistantMessage = aiMessage;
                
                // Announce to screen readers that a new message is available
                var preview = aiMessage.Content?.Length > 100 
                    ? aiMessage.Content.Substring(0, 100) + "..." 
                    : aiMessage.Content ?? "";
                SemanticScreenReader.Announce($"New message from {Character?.Name ?? "character"}: {preview}");
            });
            
            // Request scroll to bottom after AI response
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            
            _currentSession?.Messages.Add(aiMessage);

            if (_intelligenceService != null && !string.IsNullOrWhiteSpace(aiMessage.Content))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _intelligenceService.RecordInteractionAsync(
                            characterForAI,
                            MemoryType.Chat,
                            userMsg,
                            aiMessage.Content,
                            userMsg);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Intelligence record error: {ex.Message}");
                    }
                });
            }

            // Fetch contextual Bible references in background (only if enabled)
            if (_enableContextualReferences)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var references = await _bibleLookupService.GetCharacterReferencesAsync(Character, userMsg);
                        if (references.Any())
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                aiMessage.ContextualReferences = references
                                    .Select(r => new ContextualReference
                                    {
                                        Reference = r.Reference,
                                        Summary = r.Summary,
                                        Connection = r.Connection
                                    })
                                    .ToList();
                            });
                            
                            // Save updated session with references
                            if (_currentSession != null)
                            {
                                await _chatRepository.SaveSessionAsync(_currentSession);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Error fetching contextual references: {ex.Message}");
                    }
                });
            }

            if (_currentSession != null)
            {
                await _chatRepository.SaveSessionAsync(_currentSession);
            }
            
            // Record this interaction to build user memory (in background)
            if (_personalizedPromptService != null && Character != null && !string.IsNullOrEmpty(aiMessage.Content))
            {
                var userId = _userService.CurrentUser?.Id ?? "default";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _personalizedPromptService.RecordInteractionAsync(
                            userId, 
                            Character.Id, 
                            userMsg, 
                            aiMessage.Content);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Recorded interaction for user memory");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Error recording interaction: {ex.Message}");
                    }
                });
            }
            
            // Track user progression (analyzes message for growth signals)
            if (_progressionService != null && !string.IsNullOrEmpty(aiMessage.Content))
            {
                var userId = _userService.CurrentUser?.Id ?? "default";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _progressionService.RecordConversationAsync(
                            userId, 
                            userMsg, 
                            aiMessage.Content,
                            Character?.Id);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Recorded conversation for user progression tracking");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Error tracking progression: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] SendMessage ERROR: {ex}");
            var userFriendlyError = UserFriendlyErrors.GetUserFriendlyMessage(ex);
            ShowModelWarningIfRelevant(userFriendlyError);
            var errorMessage = new ChatMessage
            {
                Role = "assistant",
                Content = $"⚠️ {userFriendlyError}",
                Timestamp = DateTime.UtcNow
            };
            Messages.Add(errorMessage);
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine("[DEBUG] Setting IsAiTyping = false");
            IsAiTyping = false;
            _aiResponseCancellationTokenSource?.Dispose();
            _aiResponseCancellationTokenSource = null;
        }
    }

    private static BiblicalCharacter CloneCharacterWithPrompt(BiblicalCharacter source, string prompt)
    {
        return new BiblicalCharacter
        {
            Id = source.Id,
            Name = source.Name,
            Title = source.Title,
            Description = source.Description,
            Era = source.Era,
            BiblicalReferences = new List<string>(source.BiblicalReferences),
            SystemPrompt = prompt,
            Attributes = new Dictionary<string, string>(source.Attributes),
            IconFileName = source.IconFileName,
            Voice = source.Voice,
            PrimaryTone = source.PrimaryTone,
            Relationships = new Dictionary<string, string>(source.Relationships),
            PrayerStyle = source.PrayerStyle,
            IsCustom = source.IsCustom,
            RoundtableEnabled = source.RoundtableEnabled,
            IsContrarian = source.IsContrarian
        };
    }

    private void ShowModelWarningIfRelevant(string message)
    {
        if (string.Equals(message, UserFriendlyErrors.OllamaConnectionError, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(message, UserFriendlyErrors.OfflineMode, StringComparison.OrdinalIgnoreCase))
        {
            IsModelWarningVisible = true;
            ModelWarningText = message;
        }
    }

    private void ClearModelWarning()
    {
        IsModelWarningVisible = false;
        ModelWarningText = string.Empty;
    }

    [RelayCommand]
    private void CancelResponse()
    {
        if (_aiResponseCancellationTokenSource != null && !_aiResponseCancellationTokenSource.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine("[DEBUG] Cancelling AI response...");
            _aiResponseCancellationTokenSource.Cancel();
            
            // Immediately update UI to show response was stopped
            IsAiTyping = false;
            
            // Add cancellation indicator to the last message if it's from the assistant
            var lastMessage = Messages.LastOrDefault();
            if (lastMessage != null && lastMessage.Role == "assistant" && !lastMessage.Content.EndsWith("[Stopped]"))
            {
                lastMessage.Content += "\n\n[Stopped]";
            }
        }
    }

    [RelayCommand]
    private async Task ToggleVoice(ChatMessage? message)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] ToggleVoice called, message is null: {message == null}");
        
        if (message == null || string.IsNullOrEmpty(message.Content) || message.Role != "assistant")
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] ToggleVoice early return - message null: {message == null}, content empty: {string.IsNullOrEmpty(message?.Content)}, role: {message?.Role}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[DEBUG] ToggleVoice proceeding with message: {message.Content.Substring(0, Math.Min(50, message.Content.Length))}...");

        // If we're already speaking this message, stop it
        if (IsSpeaking && SpeakingMessageId == message.Id)
        {
            await StopSpeakingAsync();
            return;
        }

        // If speaking a different message, stop that first
        if (IsSpeaking)
        {
            await StopSpeakingAsync();
        }

        // Start speaking this message
        await SpeakMessageAsync(message);
    }

    private async Task SpeakMessageAsync(ChatMessage message)
    {
        if (Character == null) return;

        try
        {
            IsSpeaking = true;
            SpeakingMessageId = message.Id;
            _currentlySpeakingMessage = message;
            _voiceCancellationTokenSource = new CancellationTokenSource();

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Speaking message with voice: {Character.Voice.Description}");
            
            await _voiceService.SpeakAsync(
                message.Content,
                Character.Voice,
                _voiceCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when user cancels
            System.Diagnostics.Debug.WriteLine("[DEBUG] Voice playback cancelled by user");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Voice playback error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Voice Error", $"Could not play voice: {ex.Message}");
        }
        finally
        {
            IsSpeaking = false;
            SpeakingMessageId = null;
            _currentlySpeakingMessage = null;
            _voiceCancellationTokenSource?.Dispose();
            _voiceCancellationTokenSource = null;
        }
    }

    private async Task StopSpeakingAsync()
    {
        if (_voiceCancellationTokenSource != null && !_voiceCancellationTokenSource.IsCancellationRequested)
        {
            _voiceCancellationTokenSource.Cancel();
        }
        await _voiceService.StopSpeakingAsync();
        IsSpeaking = false;
        SpeakingMessageId = null;
        _currentlySpeakingMessage = null;
    }

    [RelayCommand]
    private async Task StopVoice()
    {
        await StopSpeakingAsync();
    }

    [RelayCommand]
    private async Task RateMessage((ChatMessage message, int targetRating) args)
    {
        var (message, targetRating) = args;
        if (message == null || message.Role != "assistant") return;
        
        // Toggle rating: if already at target rating, remove it; otherwise set it
        var newRating = message.Rating == targetRating ? 0 : targetRating;
        message.Rating = newRating;
        System.Diagnostics.Debug.WriteLine($"[DEBUG] Rated message {message.Id} as {message.Rating}");
        
        // If rated (not removing rating), optionally ask for feedback
        if (newRating != 0)
        {
            var provideFeedback = await _dialogService.ShowConfirmAsync(
                "Feedback",
                "Would you like to explain why?",
                "Yes", "No");
            
            if (provideFeedback)
            {
                var prompt = newRating == 1 
                    ? "What made this response helpful?" 
                    : "How could this response be improved?";
                    
                var feedback = await _dialogService.ShowPromptAsync(
                    "Your Feedback",
                    prompt,
                    maxLength: 500);
                
                if (!string.IsNullOrWhiteSpace(feedback))
                {
                    message.Feedback = feedback;
                }
            }
        }
        else
        {
            // Clearing rating also clears feedback
            message.Feedback = null;
        }
        
        // Save the updated session with rating
        if (_currentSession != null)
        {
            await _chatRepository.SaveSessionAsync(_currentSession);
        }
    }

    [RelayCommand]
    private async Task ClearChat()
    {
        if (Character != null)
        {
            Messages.Clear();
            await InitializeAsync(Character);
        }
    }

    [RelayCommand]
    private async Task SaveToReflections(ChatMessage? message)
    {
        if (message == null || string.IsNullOrEmpty(message.Content)) return;

        try
        {
            IsSavingReflection = true;
            IsActionInProgress = true;
            
            var title = await _dialogService.ShowPromptAsync(
                "Save to Reflections",
                "Give this reflection a title:",
                initialValue: $"Chat with {Character?.Name ?? "Character"}",
                maxLength: 100);

            if (title == null)
            {
                IsSavingReflection = false;
                IsActionInProgress = false;
                return; // Cancelled
            }

            // Get Bible references from contextual references if any
            var bibleRefs = message.ContextualReferences?
                .Select(r => r.Reference)
                .ToList() ?? new List<string>();

            var reflection = new Reflection
            {
                Title = title,
                SavedContent = message.Content,
                Type = ReflectionType.Chat,
                CharacterName = Character?.Name,
                BibleReferences = bibleRefs,
                CreatedAt = DateTime.UtcNow
            };

            await _reflectionRepository.SaveReflectionAsync(reflection);

            IsSavingReflection = false;
            IsActionInProgress = false;
            
            var goToReflections = await _dialogService.ShowConfirmAsync(
                "Saved! ✓",
                "This response has been saved to your reflections. Would you like to add your thoughts now?",
                "Go to Reflections", "Stay Here");
                
            if (goToReflections)
            {
                await Shell.Current.GoToAsync("//reflections");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Error saving reflection: {ex.Message}");
        }
        finally
        {
            IsSavingReflection = false;
            IsActionInProgress = false;
        }
    }

    [RelayCommand]
    private async Task GeneratePrayerFromMessage(ChatMessage? message)
    {
        if (message == null || string.IsNullOrEmpty(message.Content) || Character == null) return;

        // Find the user's question that prompted this response
        var messageIndex = Messages.IndexOf(message);
        string userQuestion = "";
        if (messageIndex > 0)
        {
            for (int i = messageIndex - 1; i >= 0; i--)
            {
                if (Messages[i].Role == "user")
                {
                    userQuestion = Messages[i].Content;
                    break;
                }
            }
        }

        try
        {
            IsGeneratingPrayer = true;
            IsActionInProgress = true;

            // Create a simple topic based on the conversation
            var prayerTopic = $"A prayer inspired by {Character.Name}'s wisdom about: {userQuestion}";

            var prayer = await _aiService.GeneratePrayerAsync(prayerTopic);

            if (string.IsNullOrEmpty(prayer))
            {
                await _dialogService.ShowAlertAsync("Error", "Could not generate prayer. Please try again.");
                return;
            }

            // Show the prayer in a dialog with option to save
            var savePrayer = await _dialogService.ShowConfirmAsync(
                $"🙏 Prayer from {Character.Name}",
                prayer,
                "Save Prayer", "Close");

            if (savePrayer)
            {
                // Save to prayers
                var prayerEntity = new Prayer
                {
                    Topic = $"Prayer with {Character.Name}",
                    Content = prayer,
                    CreatedAt = DateTime.UtcNow
                };
                await _prayerRepository.SavePrayerAsync(prayerEntity);

                _usageMetrics?.TrackPrayerGenerated(userQuestion);

                await _dialogService.ShowAlertAsync("Saved! ✓", "Prayer saved to your prayer history.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Error generating prayer: {ex.Message}");
            var userFriendlyError = UserFriendlyErrors.GetUserFriendlyMessage(ex);
            await _dialogService.ShowAlertAsync("Error", userFriendlyError);
        }
        finally
        {
            IsGeneratingPrayer = false;
            IsActionInProgress = false;
        }
    }

    public void Dispose()
    {
        // Cancel any ongoing operations
        _speechCancellationTokenSource?.Cancel();
        _aiResponseCancellationTokenSource?.Cancel();
        _voiceCancellationTokenSource?.Cancel();

        // Dispose cancellation token sources
        _speechCancellationTokenSource?.Dispose();
        _aiResponseCancellationTokenSource?.Dispose();
        _voiceCancellationTokenSource?.Dispose();

        // Clear references
        _speechCancellationTokenSource = null;
        _aiResponseCancellationTokenSource = null;
        _voiceCancellationTokenSource = null;
    }
}
