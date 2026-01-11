using System.Collections.ObjectModel;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class RoundtableChatViewModel : BaseViewModel
{
    private readonly IMultiCharacterChatService _multiCharacterChatService;
    private readonly ICharacterRepository _characterRepository;
    private readonly IChatRepository _chatRepository;
    private readonly ICrossCharacterLearningService _learningService;

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
    private bool _canSend = true;

    [ObservableProperty]
    private bool _isDiscussionMode = true; // Default to discussion mode

    [ObservableProperty]
    private bool _isDiscussionActive;

    [ObservableProperty]
    private bool _isWaitingForUserInput;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _userInputPrompt = string.Empty;

    [ObservableProperty]
    private string? _discussionOutcome;
    
    [ObservableProperty]
    private bool _isLearningEnabled = true; // Enable character evolution by default

    // Computed properties for UI
    public bool HasMessages => Messages?.Count > 0;
    public bool HasNoMessages => Messages?.Count == 0;

    private ChatSession? _session;
    private CancellationTokenSource? _discussionCancellation;
    private string _currentTopic = string.Empty;

    public RoundtableChatViewModel(
        IMultiCharacterChatService multiCharacterChatService,
        ICharacterRepository characterRepository,
        IChatRepository chatRepository,
        ICrossCharacterLearningService learningService)
    {
        _multiCharacterChatService = multiCharacterChatService;
        _characterRepository = characterRepository;
        _chatRepository = chatRepository;
        _learningService = learningService;
        
        Title = "Roundtable Discussion";
    }

    public async Task InitializeAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SessionId)) return;

        try
        {
            IsBusy = true;

            // Load session
            _session = await _chatRepository.GetSessionAsync(SessionId);
            if (_session == null)
            {
                await Shell.Current.DisplayAlert("Error", "Session not found", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Load characters
            var characterList = new List<BiblicalCharacter>();
            foreach (var characterId in _session.ParticipantCharacterIds)
            {
                var character = await _characterRepository.GetCharacterAsync(characterId);
                if (character != null)
                {
                    characterList.Add(character);
                }
            }
            Characters = new ObservableCollection<BiblicalCharacter>(characterList);

            // Load existing messages and populate character names
            foreach (var msg in _session.Messages.Where(m => m.Role == "assistant"))
            {
                var character = characterList.FirstOrDefault(c => c.Id == msg.CharacterId);
                if (character != null)
                {
                    msg.CharacterName = character.Name;
                }
            }
            
            Messages = new ObservableCollection<ChatMessage>(_session.Messages);
            OnPropertyChanged(nameof(HasMessages));
            OnPropertyChanged(nameof(HasNoMessages));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to initialize: {ex.Message}", "OK");
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
                if (_session != null)
                {
                    _session.Messages.Clear();
                }
                OnPropertyChanged(nameof(HasMessages));
                OnPropertyChanged(nameof(HasNoMessages));

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
            await Shell.Current.DisplayAlert("Error", $"Discussion error: {ex.Message}", "OK");
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
                            // Ensure character name is populated
                            if (update.Message.Role == "assistant" && string.IsNullOrEmpty(update.Message.CharacterName))
                            {
                                var character = Characters.FirstOrDefault(c => c.Id == update.Message.CharacterId);
                                if (character != null)
                                {
                                    update.Message.CharacterName = character.Name;
                                }
                            }
                            
                            Messages.Add(update.Message);
                            OnPropertyChanged(nameof(HasMessages));
                            OnPropertyChanged(nameof(HasNoMessages));
                            
                            // Save to session
                            if (_session != null)
                            {
                                _session.Messages.Add(update.Message);
                            }
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
            if (_session != null)
            {
                await _chatRepository.SaveSessionAsync(_session);
            }
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

            // Get roundtable responses from all characters
            var responses = await _multiCharacterChatService.GetRoundtableResponsesAsync(
                Characters.ToList(),
                Messages.ToList(),
                userMessage);

            // Populate character names for display
            foreach (var response in responses.Where(r => r.Role == "assistant"))
            {
                var character = Characters.FirstOrDefault(c => c.Id == response.CharacterId);
                if (character != null)
                {
                    response.CharacterName = character.Name;
                }
            }

            // Add all responses to UI
            foreach (var response in responses)
            {
                Messages.Add(response);
            }
            OnPropertyChanged(nameof(HasMessages));
            OnPropertyChanged(nameof(HasNoMessages));

            // Save to session
            if (_session != null)
            {
                _session.Messages.AddRange(responses);
                await _chatRepository.SaveSessionAsync(_session);
            }
            
            // Process learning if enabled - characters learn from each other
            if (IsLearningEnabled && responses.Count > 1)
            {
                try
                {
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
            await Shell.Current.DisplayAlert("Error", $"Failed to get responses: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleDiscussionMode()
    {
        IsDiscussionMode = !IsDiscussionMode;
    }

    [RelayCommand]
    private void CancelDiscussion()
    {
        _discussionCancellation?.Cancel();
        IsDiscussionActive = false;
        IsWaitingForUserInput = false;
        StatusMessage = "";
    }

    [RelayCommand]
    private async Task ContinueDiscussion()
    {
        if (!IsWaitingForUserInput) return;

        InputText = "continue";
        await SendMessage();
    }

    [RelayCommand]
    private async Task ConcludeDiscussion()
    {
        if (!IsWaitingForUserInput && !IsDiscussionActive) return;

        InputText = "conclude";
        await SendMessage();
    }
}
