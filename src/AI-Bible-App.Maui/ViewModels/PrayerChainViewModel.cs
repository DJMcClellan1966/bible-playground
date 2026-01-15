using System.Collections.ObjectModel;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class PrayerChainViewModel : BaseViewModel
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
    private ObservableCollection<ChatMessage> _prayers = new();

    [ObservableProperty]
    private string _prayerTopic = string.Empty;

    [ObservableProperty]
    private bool _canStartPrayer = true;

    private ChatSession? _session;

    public PrayerChainViewModel(
        IMultiCharacterChatService multiCharacterChatService,
        ICharacterRepository characterRepository,
        IChatRepository chatRepository,
        IUsageMetricsService? usageMetrics = null)
    {
        _multiCharacterChatService = multiCharacterChatService;
        _characterRepository = characterRepository;
        _chatRepository = chatRepository;
        _usageMetrics = usageMetrics;
        
        Title = "Prayer Chain";
    }

    public async Task InitializeAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SessionId)) return;

        try
        {
            IsBusy = true;
            _usageMetrics?.TrackFeatureUsed("PrayerChain");

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

            // Load existing prayers if any
            if (_session.Messages.Any())
            {
                // Skip the user request message, show only character prayers
                var existingPrayers = _session.Messages.Where(m => m.Role == "assistant");
                
                // Populate character names
                foreach (var prayer in existingPrayers)
                {
                    var character = characterList.FirstOrDefault(c => c.Id == prayer.CharacterId);
                    if (character != null)
                    {
                        prayer.CharacterName = character.Name;
                    }
                }
                
                Prayers = new ObservableCollection<ChatMessage>(existingPrayers);
                CanStartPrayer = false;
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
    private async Task StartPrayerChain()
    {
        if (string.IsNullOrWhiteSpace(PrayerTopic) || IsBusy || !CanStartPrayer) return;

        try
        {
            IsBusy = true;
            CanStartPrayer = false;

            var topic = PrayerTopic.Trim();
            PrayerTopic = string.Empty;

            // Get prayers from all characters in sequence
            var chainPrayers = await _multiCharacterChatService.GetPrayerChainResponsesAsync(
                Characters.ToList(),
                topic);

            // Skip the user request message, populate character names, and add to UI
            var characterPrayers = chainPrayers.Where(m => m.Role == "assistant");
            foreach (var prayer in characterPrayers)
            {
                var character = Characters.FirstOrDefault(c => c.Id == prayer.CharacterId);
                if (character != null)
                {
                    prayer.CharacterName = character.Name;
                }
                Prayers.Add(prayer);
            }

            // Save to session
            if (_session != null)
            {
                _session.Messages.AddRange(chainPrayers);
                await _chatRepository.SaveSessionAsync(_session);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to start prayer chain: {ex.Message}", "OK");
            CanStartPrayer = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
