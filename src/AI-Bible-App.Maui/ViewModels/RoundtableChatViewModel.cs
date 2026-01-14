using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

// Add QueryProperty attribute and open class
[QueryProperty(nameof(SessionId), "sessionId")]
public partial class RoundtableChatViewModel : BaseViewModel
{
    // Helper to update HasMessages/HasNoMessages
    private void UpdateMessageProperties()
    {
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(HasNoMessages));
    }

    // Utility to assign character names to messages
    private void AssignCharacterNames(IEnumerable<ChatMessage> messages, IEnumerable<BiblicalCharacter> characters)
    {
        var charDict = characters.ToDictionary(c => c.Id, c => c.Name);
        foreach (var msg in messages)
        {
            if (msg == null || msg.Role != "assistant") continue;
            if (!string.IsNullOrEmpty(msg.CharacterId) && charDict.TryGetValue(msg.CharacterId, out var name))
            {
                msg.CharacterName = name;
            }
        }
    }

    // Extract session loading and error handling
    private async Task<ChatSession?> TryLoadSessionAsync(string sessionId)
    {
        try
        {
            var session = await _chatRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                await ShowAlertAsync("Error", "Session not found");
                await Shell.Current.GoToAsync("..");
                return null;
            }
            return session;
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Failed to load session: {ex.Message}");
            return null;
        }
    }

    // Centralized error handler
    private async Task HandleErrorAsync(Exception ex, string context = "")
    {
        var msg = string.IsNullOrEmpty(context) ? ex.Message : $"{context}: {ex.Message}";
        await ShowAlertAsync("Error", msg);
    }

    // Contrarian management helpers
    private void AddContrarian(BiblicalCharacter character)
    {
        _session.ContrarianCharacterIds ??= new List<string>();
        if (!string.IsNullOrEmpty(character.Id) && !_session.ContrarianCharacterIds.Contains(character.Id))
            _session.ContrarianCharacterIds.Add(character.Id);
    }

    private void RemoveContrarian(BiblicalCharacter character)
    {
        _session.ContrarianCharacterIds ??= new List<string>();
        _session.ContrarianCharacterIds.RemoveAll(id => id == character.Id);
    }

    // Background task handler for fire-and-forget async calls
    private void RunBackgroundTask(Func<Task> taskFunc)
    {
        Task.Run(async () =>
        {
            try { await taskFunc(); }
            catch (Exception) { /* log or ignore */ }
        });
    }

    // Abstract UI alert for testability
    private Task ShowAlertAsync(string title, string message) => Shell.Current.DisplayAlert(title, message, "OK");
    private readonly IMultiCharacterChatService _multiCharacterChatService;
    private readonly ICharacterRepository _characterRepository;
    private readonly IChatRepository _chatRepository;
    private readonly ICrossCharacterLearningService _learningService;
    private readonly IPromptTemplateService? _promptService;
    private readonly IRetrievalService? _retrievalService;
    private readonly IContextCompressor? _compressor;
    private readonly IModelOrchestrator? _modelOrchestrator;
    private readonly IUnconsciousService? _unconsciousService;

    [ObservableProperty]
    private string _unconsciousStatus = string.Empty;

    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    [NotifyPropertyChangedFor(nameof(HasNoMessages))]
    private ObservableCollection<BiblicalCharacter> _characters = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    [NotifyPropertyChangedFor(nameof(HasNoMessages))]
    private ObservableCollection<ChatMessage> _messages = new();

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isDevilsAdvocateEnabled;

    [ObservableProperty]
    private string _advocateTone = "soft"; // soft, firm, strong

    [ObservableProperty]
    private bool _canSend = true;

    [ObservableProperty]
    private bool _isDiscussionMode = true; // Default to discussion mode

    [ObservableProperty]
    private bool _isDiscussionActive;

    [ObservableProperty]
    private bool _isWaitingForUserInput;

    /// <summary>
    /// Indicates if there are any messages in the chat.
    /// </summary>
    public bool HasMessages => Messages?.Count > 0;
    /// <summary>
    /// Indicates if there are no messages in the chat.
    /// </summary>
    public bool HasNoMessages => Messages?.Count == 0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _userInputPrompt = string.Empty;

    [ObservableProperty]
    private string? _discussionOutcome = null;

    [ObservableProperty]
    private bool _isLearningEnabled = true; // Enable character evolution by default

    private ChatSession _session = null!; // Set after session load, never used before
    private CancellationTokenSource? _discussionCancellation = null;
    private string _currentTopic = string.Empty;

        public RoundtableChatViewModel(
            IMultiCharacterChatService multiCharacterChatService,
            ICharacterRepository characterRepository,
            IChatRepository chatRepository,
            ICrossCharacterLearningService learningService,
            IPromptTemplateService? promptService = null,
            IRetrievalService? retrievalService = null,
            IContextCompressor? compressor = null,
            IModelOrchestrator? modelOrchestrator = null,
            IUnconsciousService? unconsciousService = null)
        {
            _multiCharacterChatService = multiCharacterChatService;
            _characterRepository = characterRepository;
            _chatRepository = chatRepository;
            _learningService = learningService;
            _promptService = promptService;
            _retrievalService = retrievalService;
            _compressor = compressor;
            _modelOrchestrator = modelOrchestrator;
            _unconsciousService = unconsciousService;
            if (_unconsciousService != null)
            {
                _unconsciousService.ConsolidationCompleted += id =>
                {
                    UnconsciousStatus = $"Consolidated:{DateTime.UtcNow:O}";
                };
            }
            
            Title = "Roundtable Discussion";
        }

        public async Task InitializeAsync()
        {
            if (IsBusy || string.IsNullOrEmpty(SessionId)) return;

            try
            {
                IsBusy = true;

                // Step 1: Use extracted session loader
                var session = await TryLoadSessionAsync(SessionId);
                if (session == null) return;

                // Load characters
                var characterList = new List<BiblicalCharacter>();
                foreach (var characterId in session.ParticipantCharacterIds)
                {
                    var character = await _characterRepository.GetCharacterAsync(characterId);
                    if (character != null)
                    {
                        characterList.Add(character);
                    }
                }
                Characters = new ObservableCollection<BiblicalCharacter>(characterList);

                // Restore session-level devil's advocate settings
                IsDevilsAdvocateEnabled = session.DevilsAdvocateEnabled;
                AdvocateTone = session.AdvocateTone ?? "soft";
                // Mark contrarian characters from session
                var contrarianIds = session.ContrarianCharacterIds ?? new List<string>();
                foreach (var ch in Characters!)
                {
                    if (ch == null) continue;
                    ch.IsContrarian = contrarianIds.Contains(ch.Id);
                }

                // Load existing messages and assign character names
                var sessionMessages = session.Messages ?? new List<ChatMessage>();
                AssignCharacterNames(sessionMessages, characterList);
                Messages = new ObservableCollection<ChatMessage>(sessionMessages);
                UpdateMessageProperties();

                // Only assign to _session after all null checks and setup
                _session = session;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, "InitializeAsync");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsBusy || !CanSend) return;

            var userMessage = InputText.Trim();
            InputText = string.Empty;

            if (IsDiscussionMode)
            {
                await HandleDiscussionInput(userMessage);
            }
            else
            {
                await HandleStandardRoundtable(userMessage);
            }
        }

        [RelayCommand]
        private async Task ToggleContrarian(BiblicalCharacter character)
        {
            if (character == null) return;

            character.IsContrarian = !character.IsContrarian;
            if (character.IsContrarian)
                AddContrarian(character);
            else
                RemoveContrarian(character);

            // Persist session settings
            try
            {
                await _chatRepository.SaveSessionAsync(_session);
            }
            catch { }
        }

        partial void OnIsDevilsAdvocateEnabledChanged(bool value)
        {
            _session.DevilsAdvocateEnabled = value;
            RunBackgroundTask(() => _chatRepository.SaveSessionAsync(_session));
        }

        partial void OnAdvocateToneChanged(string value)
        {
            _session.AdvocateTone = value;
            RunBackgroundTask(() => _chatRepository.SaveSessionAsync(_session));
        }

        private async Task HandleDiscussionInput(string userMessage)
        {
            try
            {
                IsBusy = true;
                CanSend = false;

                if (IsWaitingForUserInput)
                {
                    // Continue existing discussion with user input
                    IsWaitingForUserInput = false;
                    await ProcessDiscussionUpdatesAsync(
                        _multiCharacterChatService.ContinueDiscussionAsync(userMessage));
                }
                else if (!IsDiscussionActive)
                {
                    // Start new discussion with FRESH history
                    // CRITICAL: Clear UI messages AND session messages for fresh start
                    IsDiscussionActive = true;
                    DiscussionOutcome = null;
                    
                    // Clear ALL old messages for fresh discussion
                    Messages.Clear();
                    _session.Messages.Clear();
                    UpdateMessageProperties();

                    var settings = new DiscussionSettings
                    {
                        MaxTurnsBeforeCheck = 4,
                        MaxTotalTurns = 16,
                        SeekConsensus = true,
                        AllowUserInterjection = true
                    };

                    await ProcessDiscussionUpdatesAsync(
                        _multiCharacterChatService.StartDynamicDiscussionAsync(
                            Characters.ToList(),
                            new List<ChatMessage>(), // FRESH start - no old history
                            userMessage,
                            settings));
                }
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, "Discussion error");
                IsDiscussionActive = false;
            }
            finally
            {
                IsBusy = false;
                CanSend = true;
            }
        }

        private async Task ProcessDiscussionUpdatesAsync(IAsyncEnumerable<DiscussionUpdate> updates)
        {
            if (_discussionCancellation != null)
            {
                _discussionCancellation.Dispose();
            }
            _discussionCancellation = new CancellationTokenSource();

            try
            {
                await foreach (var update in updates.WithCancellation(_discussionCancellation.Token))
                {
                    switch (update.Type)
                    {
                        case DiscussionUpdateType.CharacterSpeaking:
                        case DiscussionUpdateType.StatusUpdate:
                            StatusMessage = update.StatusMessage ?? "";
                            break;

                        case DiscussionUpdateType.CharacterResponse:
                            if (update.Message != null)
                            {
                                // Assign character name using utility
                                AssignCharacterNames(new[] { update.Message }, Characters);
                                Messages.Add(update.Message);
                                UpdateMessageProperties();
                                // Save to session
                                _session.Messages.Add(update.Message);
                            }
                            StatusMessage = "";
                            break;

                        case DiscussionUpdateType.RequestingUserInput:
                            IsWaitingForUserInput = true;
                            UserInputPrompt = update.UserInputPrompt ?? "Add your thoughts...";
                            StatusMessage = update.StatusMessage ?? "";
                            break;

                        case DiscussionUpdateType.ConsensusReached:
                        case DiscussionUpdateType.NoConsensus:
                        case DiscussionUpdateType.DiscussionComplete:
                            IsDiscussionActive = false;
                            IsWaitingForUserInput = false;
                            DiscussionOutcome = update.StatusMessage;
                            StatusMessage = "";
                            break;
                    }

                    // Small delay for UI updates
                    await Task.Delay(100);
                }

                // Save session after discussion round
                await _chatRepository.SaveSessionAsync(_session);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Discussion paused.";
            }
        }

        private async Task HandleStandardRoundtable(string userMessage)
        {
            try
            {
                IsBusy = true;
                
                // Track the topic for learning
                if (string.IsNullOrEmpty(_currentTopic))
                {
                    _currentTopic = userMessage;
                }

                // Prepare conversation history (may include retrieved context)
                var conversationHistory = Messages.ToList();

                // Fire-and-forget: call unconscious preparer to build compact context in background
                if (_unconsciousService != null)
                {
                    var prepareTask = _unconsciousService.PrepareContextAsync(_session.Id, userMessage);
                    // Use result if it completes synchronously/quickly
                    if (prepareTask.IsCompletedSuccessfully)
                    {
                        var sys = prepareTask.Result;
                        if (!string.IsNullOrWhiteSpace(sys))
                        {
                            conversationHistory.Insert(0, new ChatMessage { Role = "system", Content = sys });
                        }
                    }
                    else
                    {
                        _ = prepareTask.ContinueWith(t => {
                            if (t.Status == TaskStatus.RanToCompletion && !string.IsNullOrWhiteSpace(t.Result))
                            {
                                // Best-effort: we don't update UI here to avoid cross-thread complexity
                            }
                        });
                    }
                }

                if (_retrievalService != null)
                {
                    try
                    {
                        var docs = await _retrievalService.RetrieveAsync(userMessage, 3);
                        var contextText = string.Join("\n\n", docs.Select(d => d.Content));
                        if (!string.IsNullOrWhiteSpace(contextText))
                        {
                            if (_compressor != null)
                            {
                                contextText = _compressor.Compress(contextText);
                            }

                            var systemPrompt = _promptService?.RenderTemplate("roundtable_system_context", new Dictionary<string, object?>
                            {
                                ["context"] = contextText,
                                ["topic"] = userMessage
                            }) ?? $"Context: {contextText}";

                            conversationHistory.Insert(0, new ChatMessage { Role = "system", Content = systemPrompt });
                        }
                    }
                    catch
                    {
                        // Retrieval is best-effort; don't block main flow
                    }
                }

                // Get roundtable responses from all characters
                var responses = await _multiCharacterChatService.GetRoundtableResponsesAsync(
                    Characters.ToList(),
                    conversationHistory,
                    userMessage,
                    enableDevilsAdvocate: IsDevilsAdvocateEnabled,
                    advocateTone: AdvocateTone);


                // Assign character names and add responses to UI
                AssignCharacterNames(responses, Characters);
                foreach (var response in responses)
                {
                    Messages.Add(response);
                }
                UpdateMessageProperties();

                // Save to session
                _session.Messages.AddRange(responses);
                await _chatRepository.SaveSessionAsync(_session);
                // Best-effort: consolidate recent messages into unconscious short-term memory
                if (_unconsciousService != null)
                {
                    var recent = responses;
                    RunBackgroundTask(() => _unconsciousService.ConsolidateAsync(_session.Id, recent));
                }
                // Process learning if enabled - characters learn from each other
                if (IsLearningEnabled && responses.Count > 1)
                {
                    try
                    {
                        // TODO: Show a more visible/persistent UI indicator for background learning
                        await _learningService.ProcessRoundtableDiscussionAsync(
                            _currentTopic,
                            Characters.ToList(),
                            Messages.ToList());
                        StatusMessage = "✨ Characters are learning from this discussion...";
                        await Task.Delay(2000);
                        StatusMessage = "";
                    }
                    catch
                    {
                        // Learning is a background feature, don't interrupt the user
                    }
                }
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, "Failed to get responses");
            }
            finally
            {
                IsBusy = false;
            }
        }

    // ...rest of the class remains unchanged...
}
