using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

namespace AI_Bible_App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Notion-style home page.
/// Provides organized access to characters, chats, and prayers.
/// </summary>
public partial class HomeViewModel : BaseViewModel
{
    public static event Action? ChatCreated;
    private readonly ICharacterRepository _characterRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IPrayerRepository _prayerRepository;
    private readonly IUserService _userService;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationService _authService;
    private readonly IDialogService _dialogService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private string greeting = "Good morning";

    [ObservableProperty]
    private string userName = "User";

    [ObservableProperty]
    private string userEmoji = "👤";

    [ObservableProperty]
    private ObservableCollection<BiblicalCharacter> featuredCharacters = new();

    [ObservableProperty]
    private ObservableCollection<ChatSession> recentChats = new();

    [ObservableProperty]
    private ObservableCollection<Prayer> recentPrayers = new();

    public HomeViewModel(
        ICharacterRepository characterRepository,
        IChatRepository chatRepository,
        IPrayerRepository prayerRepository,
        IUserService userService,
        INavigationService navigationService,
        IAuthenticationService authService,
        IDialogService dialogService,
        IUsageMetricsService? usageMetrics = null)
    {
        _characterRepository = characterRepository;
        _chatRepository = chatRepository;
        _prayerRepository = prayerRepository;
        _userService = userService;
        _navigationService = navigationService;
        _authService = authService;
        _dialogService = dialogService;
        _usageMetrics = usageMetrics;
        
        Title = "Home";
        UpdateGreeting();

        // Subscribe to chat creation event for auto-refresh
        ChatCreated += async () => await RefreshRecentChatsAsync();
    }

    private void UpdateGreeting()
    {
        var hour = DateTime.Now.Hour;
        Greeting = hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            < 21 => "Good evening",
            _ => "Good night"
        };

        var user = _userService.CurrentUser;
        if (user != null)
        {
            UserName = user.Name;
            UserEmoji = user.AvatarEmoji ?? "👤";
        }
        else
        {
            var savedUsername = Preferences.Get("saved_username", string.Empty);
            UserName = string.IsNullOrWhiteSpace(savedUsername) ? "User" : savedUsername;
            UserEmoji = "👤";
        }
    }

    public async Task LoadDataAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            _usageMetrics?.TrackFeatureUsed("Home");
            UpdateGreeting();

            // Load featured characters (first 8)
            var characters = await _characterRepository.GetAllCharactersAsync();
            var readyCharacters = characters.Where(IsCharacterReady).ToList();
            FeaturedCharacters = new ObservableCollection<BiblicalCharacter>(
                readyCharacters.Take(8));

            // Load recent chats (last 5)
            var recentChats = await BuildRecentChatsAsync();
            RecentChats = new ObservableCollection<ChatSession>(recentChats);

            // ...existing code...

            // Load recent prayers (last 6)
            var userId = _userService.CurrentUser?.Id ?? "default";
            var allPrayers = await _prayerRepository.GetAllPrayersAsync();
            var userPrayers = allPrayers.Where(p => p.UserId == userId || string.IsNullOrEmpty(p.UserId));
            RecentPrayers = new ObservableCollection<Prayer>(
                userPrayers.OrderByDescending(p => p.CreatedAt).Take(6));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Error loading data: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NewChat()
    {
        try
        {
            await _navigationService.NavigateToAsync("//characters");
            // Fire event to auto-refresh recent chats
            ChatCreated?.Invoke();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Navigation Error", $"Could not open Characters: {ex.Message}");
        }
    }

    public async Task RefreshRecentChatsAsync()
    {
        var recentChats = await BuildRecentChatsAsync();
        RecentChats = new ObservableCollection<ChatSession>(recentChats);
    }

    private async Task<List<ChatSession>> BuildRecentChatsAsync()
    {
        var userId = _userService.CurrentUser?.Id ?? "default";
        var allChats = await _chatRepository.GetAllSessionsAsync();
        var userChats = allChats.Where(c => c.UserId == userId || string.IsNullOrEmpty(c.UserId)).ToList();
        var characterMap = (await _characterRepository.GetAllCharactersAsync())
            .ToDictionary(c => c.Id, c => c);

        foreach (var session in userChats)
        {
            var lastMessage = session.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Content));
            if (string.IsNullOrWhiteSpace(session.LastMessagePreview) && lastMessage != null)
            {
                session.LastMessagePreview = lastMessage.Content.Replace("\n", " ").Trim();
            }

            if (string.IsNullOrWhiteSpace(session.CharacterName) && characterMap.TryGetValue(session.CharacterId, out var character))
            {
                session.CharacterName = character.Name;
                session.CharacterEmoji = ResolveCharacterEmoji(character);
            }

            if (string.IsNullOrWhiteSpace(session.CharacterName))
            {
                session.CharacterName = session.SessionType switch
                {
                    ChatSessionType.Roundtable => "Roundtable",
                    ChatSessionType.WisdomCouncil => "Wisdom Council",
                    ChatSessionType.PrayerChain => "Prayer Chain",
                    _ => "Conversation"
                };
                session.CharacterEmoji = "👥";
            }
        }

        return userChats
            .OrderByDescending(c => c.Messages.LastOrDefault()?.Timestamp ?? c.StartedAt)
            .Take(5)
            .ToList();
    }

    private static string ResolveCharacterEmoji(BiblicalCharacter character)
    {
        if (character.Attributes != null)
        {
            if (character.Attributes.TryGetValue("Emoji", out var emoji) && !string.IsNullOrWhiteSpace(emoji))
                return emoji;
            if (character.Attributes.TryGetValue("emoji", out emoji) && !string.IsNullOrWhiteSpace(emoji))
                return emoji;
        }

        return "👤";
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
    private async Task Prayer()
    {
        try
        {
            await _navigationService.NavigateToAsync("//prayer");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Navigation Error", $"Could not open Prayer: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task Bible()
    {
        try
        {
            await _navigationService.NavigateToAsync("//BibleReader");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Navigation Error", $"Could not open Bible Reader: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ViewAllCharacters()
    {
        await Shell.Current.GoToAsync("//characters");
    }

    [RelayCommand]
    private async Task ViewAllChats()
    {
        await Shell.Current.GoToAsync("//history");
    }

    [RelayCommand]
    private async Task ViewAllPrayers()
    {
        await Shell.Current.GoToAsync("//prayer");
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        try
        {
            await _navigationService.NavigateToAsync("//settings");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Navigation Error", $"Could not open Settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SignOut()
    {
        var confirm = await _dialogService.ShowConfirmAsync(
            "Sign Out",
            "Are you sure you want to sign out? You'll need to sign in again to access your data.");

        if (!confirm)
            return;

        try
        {
            Preferences.Remove("stay_logged_in");
            Preferences.Remove("onboarding_profile");
            await _authService.SignOutAsync();
            try
            {
                await _navigationService.NavigateToAsync("//login");
            }
            catch
            {
                await Shell.Current.GoToAsync("//login");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Home] Sign out failed: {ex}");
            try
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to sign out: {ex.Message}");
            }
            catch
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    var page = Application.Current.Windows[0].Page;
                    if (page != null)
                    {
                        await page.DisplayAlert("Error", "Failed to sign out.", "OK");
                    }
                }
            }
        }
    }

    [RelayCommand]
    private async Task SelectCharacter(BiblicalCharacter character)
    {
        if (character == null) return;
        await Shell.Current.GoToAsync($"chat?characterId={character.Id}");
    }

    [RelayCommand]
    private async Task OpenChat(ChatSession session)
    {
        if (session == null) return;
        await Shell.Current.GoToAsync($"chat?sessionId={session.Id}");
    }
}
