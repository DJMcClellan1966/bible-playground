using System.Collections.ObjectModel;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

public partial class MultiCharacterSelectionViewModel : BaseViewModel
{
    private readonly ICharacterRepository _characterRepository;
    private readonly IChatRepository _chatRepository;
    private readonly INavigationService _navigationService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private ObservableCollection<SelectableCharacter> _characters = new();

    [ObservableProperty]
    private ObservableCollection<object> _selectedCharacters = new();

    [ObservableProperty]
    private ChatSessionType _selectedMode = ChatSessionType.Roundtable;

    [ObservableProperty]
    private string _modeDescription = "Multiple characters discuss the same topic";

    [ObservableProperty]
    private string _selectionInstruction = "Select 2-5 characters for the discussion:";

    [ObservableProperty]
    private string _selectedCountText = "";

    [ObservableProperty]
    private string _startButtonText = "Start Roundtable";

    [ObservableProperty]
    private bool _canStartChat;

    [ObservableProperty]
    private bool _hasSelectedCharacters;

    [ObservableProperty]
    private bool _hasNoCharacters;

    public MultiCharacterSelectionViewModel(
        ICharacterRepository characterRepository,
        IChatRepository chatRepository,
        INavigationService navigationService,
        IUsageMetricsService? usageMetrics = null)
    {
        _characterRepository = characterRepository;
        _chatRepository = chatRepository;
        _navigationService = navigationService;
        _usageMetrics = usageMetrics;
        
        Title = "Multi-Character Chat";
        
        SelectedCharacters.CollectionChanged += (s, e) => UpdateSelectionState();
    }

    public async Task InitializeAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            _usageMetrics?.TrackFeatureUsed("MultiCharacterSelection");
            HasNoCharacters = false;
            var allCharacters = await _characterRepository.GetAllCharactersAsync();
            
            System.Diagnostics.Debug.WriteLine($"[MultiCharacterSelection] Total characters loaded: {allCharacters.Count}");
            System.Diagnostics.Debug.WriteLine($"[MultiCharacterSelection] Selected mode: {SelectedMode}");
            
            // Filter characters based on selected mode
            var filteredCharacters = SelectedMode == ChatSessionType.Roundtable
                ? allCharacters.Where(c => c.RoundtableEnabled).ToList()
                : allCharacters;

            filteredCharacters = filteredCharacters.Where(IsCharacterReady).ToList();
            
            System.Diagnostics.Debug.WriteLine($"[MultiCharacterSelection] Characters with RoundtableEnabled: {allCharacters.Count(c => c.RoundtableEnabled)}");
            System.Diagnostics.Debug.WriteLine($"[MultiCharacterSelection] Filtered characters: {filteredCharacters.Count}");
            foreach (var c in allCharacters.Where(x => x.RoundtableEnabled))
            {
                System.Diagnostics.Debug.WriteLine($"[MultiCharacterSelection]   - {c.Name} (RoundtableEnabled={c.RoundtableEnabled})");
            }
            
            Characters = new ObservableCollection<SelectableCharacter>(
                filteredCharacters.Select(c => new SelectableCharacter(c)));
            HasNoCharacters = Characters.Count == 0;
        }
        catch (Exception ex)
        {
            HasNoCharacters = true;
            await Shell.Current.DisplayAlert("Error", $"Failed to load characters: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetMode(string mode)
    {
        SelectedMode = Enum.Parse<ChatSessionType>(mode);
        
        // Clear selections when mode changes
        foreach (var c in Characters)
            c.IsSelected = false;
        SelectedCharacters.Clear();
        
        UpdateModeUI();
        UpdateSelectionState();
        
        // Reload characters with appropriate filtering for the mode
        await InitializeAsync();
    }

    private static bool IsCharacterReady(BiblicalCharacter character)
    {
        if (character == null)
            return false;

        if (string.IsNullOrWhiteSpace(character.Name))
            return false;

        var description = character.Description?.Trim() ?? string.Empty;
        var title = character.Title?.Trim() ?? string.Empty;

        return description.Length >= 20 && title.Length >= 3;
    }

    [RelayCommand]
    private void ToggleCharacter(SelectableCharacter selectableCharacter)
    {
        if (selectableCharacter == null) return;
        
        // Toggle selection and force UI update
        var newValue = !selectableCharacter.IsSelected;
        selectableCharacter.IsSelected = newValue;
        
        System.Diagnostics.Debug.WriteLine($"[TOGGLE] {selectableCharacter.Character?.Name} IsSelected = {selectableCharacter.IsSelected}");
        
        if (newValue)
        {
            if (!SelectedCharacters.Contains(selectableCharacter))
            {
                SelectedCharacters.Add(selectableCharacter);
            }
        }
        else
        {
            SelectedCharacters.Remove(selectableCharacter);
        }
        
        UpdateSelectionState();
        System.Diagnostics.Debug.WriteLine($"[TOGGLE] SelectedCount now = {SelectedCharacters.Count}, CanStartChat = {CanStartChat}");
    }

    private void UpdateModeUI()
    {
        switch (SelectedMode)
        {
            case ChatSessionType.Roundtable:
                ModeDescription = "Multiple characters discuss the same topic in turn";
                SelectionInstruction = "Select 2-5 characters for the roundtable:";
                StartButtonText = "Start Roundtable";
                break;
            
            case ChatSessionType.WisdomCouncil:
                ModeDescription = "Ask one question and get perspectives from all selected characters";
                SelectionInstruction = "Select 2-5 characters for the council:";
                StartButtonText = "Start Wisdom Council";
                break;
            
            case ChatSessionType.PrayerChain:
                ModeDescription = "Characters pray in sequence, building on each other's prayers";
                SelectionInstruction = "Select 2-5 characters for the prayer chain:";
                StartButtonText = "Start Prayer Chain";
                break;
        }
    }

    private void UpdateSelectionState()
    {
        var count = SelectedCharacters.Count;
        HasSelectedCharacters = count > 0;
        SelectedCountText = $"{count} character(s) selected";
        CanStartChat = count >= 2 && count <= 5;
    }

    [RelayCommand]
    private async Task StartMultiCharacterChat()
    {
        System.Diagnostics.Debug.WriteLine($"[MULTI] StartMultiCharacterChat called - CanStartChat: {CanStartChat}, IsBusy: {IsBusy}, SelectedMode: {SelectedMode}");
        
        if (!CanStartChat || IsBusy) 
        {
            System.Diagnostics.Debug.WriteLine($"[MULTI] Early return - CanStartChat: {CanStartChat}, IsBusy: {IsBusy}");
            return;
        }

        try
        {
            IsBusy = true;
            System.Diagnostics.Debug.WriteLine($"[MULTI] Starting session creation...");

            var selectedCharacterIds = SelectedCharacters
                .OfType<SelectableCharacter>()
                .Select(sc => sc.Character.Id)
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"[MULTI] Selected {selectedCharacterIds.Count} characters");

            // Create new multi-character chat session
            var session = new ChatSession
            {
                Id = Guid.NewGuid().ToString(),
                ParticipantCharacterIds = selectedCharacterIds,
                SessionType = SelectedMode,
                StartedAt = DateTime.UtcNow,
                Messages = new List<ChatMessage>()
            };

            System.Diagnostics.Debug.WriteLine($"[MULTI] Saving session {session.Id}...");
            await _chatRepository.SaveSessionAsync(session);
            System.Diagnostics.Debug.WriteLine($"[MULTI] Session saved, navigating...");

            // Navigate to appropriate page based on mode
            switch (SelectedMode)
            {
                case ChatSessionType.Roundtable:
                    System.Diagnostics.Debug.WriteLine($"[MULTI] Navigating to RoundtableChatPage?sessionId={session.Id}");
                    await _navigationService.NavigateToAsync($"RoundtableChatPage?sessionId={session.Id}");
                    break;
                
                case ChatSessionType.WisdomCouncil:
                    await _navigationService.NavigateToAsync($"WisdomCouncilPage?sessionId={session.Id}");
                    break;
                
                case ChatSessionType.PrayerChain:
                    await _navigationService.NavigateToAsync($"PrayerChainPage?sessionId={session.Id}");
                    break;
            }
            System.Diagnostics.Debug.WriteLine($"[MULTI] Navigation complete");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to start chat: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Button color properties for visual feedback
    public Color RoundtableButtonColor => 
        SelectedMode == ChatSessionType.Roundtable 
            ? Application.Current?.Resources["Primary"] as Color ?? Colors.Blue
            : Application.Current?.Resources["Gray400"] as Color ?? Colors.Gray;

    public Color WisdomCouncilButtonColor => 
        SelectedMode == ChatSessionType.WisdomCouncil 
            ? Application.Current?.Resources["Primary"] as Color ?? Colors.Blue
            : Application.Current?.Resources["Gray400"] as Color ?? Colors.Gray;

    public Color PrayerChainButtonColor => 
        SelectedMode == ChatSessionType.PrayerChain 
            ? Application.Current?.Resources["Primary"] as Color ?? Colors.Blue
            : Application.Current?.Resources["Gray400"] as Color ?? Colors.Gray;
}
