using System.Collections.ObjectModel;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class WisdomCouncilViewModel : BaseViewModel
{
    private readonly IMultiCharacterChatService _multiCharacterChatService;
    private readonly ICharacterRepository _characterRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BiblicalCharacter> _characters = new();

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _responses = new();

    [ObservableProperty]
    private string _questionText = string.Empty;

    [ObservableProperty]
    private bool _canAsk = true;

    private ChatSession? _session;

    public WisdomCouncilViewModel(
        IMultiCharacterChatService multiCharacterChatService,
        ICharacterRepository characterRepository,
        IChatRepository chatRepository,
        IUsageMetricsService? usageMetrics = null)
    {
        _multiCharacterChatService = multiCharacterChatService;
        _characterRepository = characterRepository;
        _chatRepository = chatRepository;
        _usageMetrics = usageMetrics;
        
        Title = "Wisdom Council";
    }

    public async Task InitializeAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SessionId)) return;

        try
        {
            IsBusy = true;
            _usageMetrics?.TrackFeatureUsed("WisdomCouncil");

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

            // Load existing messages if any
            if (_session.Messages.Any())
            {
                // Populate character names
                foreach (var msg in _session.Messages.Where(m => m.Role == "assistant"))
                {
                    var character = characterList.FirstOrDefault(c => c.Id == msg.CharacterId);
                    if (character != null)
                    {
                        msg.CharacterName = character.Name;
                    }
                }
                
                Responses = new ObservableCollection<ChatMessage>(_session.Messages);
                CanAsk = false; // Already asked a question
            }
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
    private async Task AskCouncil()
    {
        if (string.IsNullOrWhiteSpace(QuestionText) || IsBusy || !CanAsk) return;

        try
        {
            IsBusy = true;
            CanAsk = false;

            var question = QuestionText.Trim();
            QuestionText = string.Empty;

            // Get responses from all council members
            var councilResponses = await _multiCharacterChatService.GetWisdomCouncilResponsesAsync(
                Characters.ToList(),
                question);

            // Populate character names for display
            foreach (var response in councilResponses.Where(r => r.Role == "assistant"))
            {
                var character = Characters.FirstOrDefault(c => c.Id == response.CharacterId);
                if (character != null)
                {
                    response.CharacterName = character.Name;
                }
            }

            // Add all responses to UI
            foreach (var response in councilResponses)
            {
                Responses.Add(response);
            }

            // Save to session
            if (_session != null)
            {
                _session.Messages.AddRange(councilResponses);
                await _chatRepository.SaveSessionAsync(_session);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to get council responses: {ex.Message}", "OK");
            CanAsk = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
