using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable MVVMTK0045 // AOT compatibility warning for WinRT scenarios

namespace AI_Bible_App.Maui.ViewModels;

public partial class CharacterSelectionViewModel : BaseViewModel
{
    private readonly ICharacterRepository _characterRepository;
    private readonly INavigationService _navigationService;
    private readonly IHealthCheckService? _healthCheckService;
    private readonly IChatRepository _chatRepository;
    private readonly IDialogService _dialogService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private ObservableCollection<BiblicalCharacter> characters = new();

    [ObservableProperty]
    private BiblicalCharacter? selectedCharacter;

    [ObservableProperty]
    private bool isOllamaOnline;

    [ObservableProperty]
    private string ollamaStatusText = "Checking AI status...";

    [ObservableProperty]
    private Color ollamaStatusColor = Colors.Gray;

    [ObservableProperty]
    private string usageSummary = string.Empty;

    public CharacterSelectionViewModel(
        ICharacterRepository characterRepository,
        INavigationService navigationService,
        IChatRepository chatRepository,
        IDialogService dialogService,
        IHealthCheckService? healthCheckService = null,
        IUsageMetricsService? usageMetrics = null)
    {
        _characterRepository = characterRepository;
        _navigationService = navigationService;
        _chatRepository = chatRepository;
        _dialogService = dialogService;
        _healthCheckService = healthCheckService;
        _usageMetrics = usageMetrics;
        Title = "Choose a Biblical Character";
    }

    public async Task InitializeAsync()
    {
        await LoadCharactersAsync();
        await CheckOllamaStatusAsync();
    }

    private async Task LoadCharactersAsync()
    {
        try
        {
            IsBusy = true;
            var chars = await _characterRepository.GetAllCharactersAsync();
            Characters = new ObservableCollection<BiblicalCharacter>(chars);
            UpdateUsageSummary(chars);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckOllamaStatusAsync()
    {
        if (_healthCheckService == null)
        {
            IsOllamaOnline = false;
            OllamaStatusText = "⚠️ Health check unavailable";
            OllamaStatusColor = Colors.Orange;
            return;
        }

        try
        {
            OllamaStatusText = "🔄 Checking AI...";
            OllamaStatusColor = Colors.Gray;
            
            var isAvailable = await _healthCheckService.IsOllamaAvailableAsync();
            IsOllamaOnline = isAvailable;
            
            if (isAvailable)
            {
                OllamaStatusText = "✅ AI Ready (Ollama Online)";
                OllamaStatusColor = Colors.Green;
            }
            else
            {
                OllamaStatusText = "❌ AI Offline - Start Ollama";
                OllamaStatusColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ollama check failed: {ex.Message}");
            IsOllamaOnline = false;
            OllamaStatusText = "❌ AI Offline - Start Ollama";
            OllamaStatusColor = Colors.Red;
        }
    }

    [RelayCommand]
    private async Task SelectCharacter(BiblicalCharacter character)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] SelectCharacter called with: {character?.Name}");
        
        if (character == null)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] SelectCharacter early exit - character is null");
            return;
        }

        if (IsBusy)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] SelectCharacter early exit - IsBusy is true");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Setting IsBusy = true");
            IsBusy = true;
            SelectedCharacter = character;
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Creating navigation parameters");
            var navParams = new Dictionary<string, object> { { "character", character } };
            
            // Check if there's an existing session for this character
            ChatSession? existingSession = null;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Checking for existing session");
                existingSession = await _chatRepository.GetLatestSessionForCharacterAsync(character.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Error loading session: {ex.Message}");
            }
            
            if (existingSession != null && existingSession.Messages.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Found existing session with {existingSession.Messages.Count} messages");
                
                // Ask user if they want to continue or start new
                var result = await _dialogService.ShowActionSheetAsync(
                    $"Continue conversation with {character.Name}?",
                    "Cancel",
                    null,
                    "Continue Chat",
                    "Start New Chat");
                
                if (result == "Cancel" || result == null)
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] User cancelled selection");
                    return;
                }
                
                if (result == "Start New Chat")
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] User chose new chat");
                    navParams.Add("newChat", true);
                }
                // "Continue Chat" will use the existing session automatically
            }
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] About to navigate to chat page...");
            await _navigationService.NavigateToAsync("chat", navParams);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Navigation completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] SelectCharacter FAILED: {ex}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] Exception details: {ex.ToString()}");
            
            try
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to open chat: {ex.Message}", "OK");
            }
            catch (Exception alertEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to show error alert: {alertEx.Message}");
            }
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Setting IsBusy = false");
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToPrayer()
    {
        await _navigationService.NavigateToAsync("prayer");
    }

    [RelayCommand]
    private async Task NavigateToMultiCharacter()
    {
        await _navigationService.NavigateToAsync("MultiCharacterSelectionPage");
    }

    private void UpdateUsageSummary(IEnumerable<BiblicalCharacter> characters)
    {
        if (_usageMetrics == null)
            return;

        var insights = _usageMetrics.GetInsights();
        if (insights.TotalConversations <= 0)
        {
            UsageSummary = "No conversations yet. Start with your favorite character!";
            return;
        }

        var favoriteName = characters
            .FirstOrDefault(c => c.Id == insights.FavoriteCharacter)?.Name ?? "—";

        UsageSummary = $"Most chatted: {favoriteName} • Conversations: {insights.TotalConversations}";
    }
}
