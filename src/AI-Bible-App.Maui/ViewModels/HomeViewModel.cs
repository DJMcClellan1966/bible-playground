using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private string userName = "Friend";

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
            FeaturedCharacters = new ObservableCollection<BiblicalCharacter>(
                characters.Take(8));

            // Load recent chats (last 5)
            var userId = _userService.CurrentUser?.Id ?? "default";
            var allChats = await _chatRepository.GetAllSessionsAsync();
            var userChats = allChats.Where(c => c.UserId == userId || string.IsNullOrEmpty(c.UserId));
            RecentChats = new ObservableCollection<ChatSession>(
                userChats.OrderByDescending(c => c.StartedAt).Take(5));

            // ...existing code...

            // Load recent prayers (last 6)
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
        await Shell.Current.GoToAsync("//characters");
        // Fire event to auto-refresh recent chats
        ChatCreated?.Invoke();
    }

    public async Task RefreshRecentChatsAsync()
    {
        var userId = _userService.CurrentUser?.Id ?? "default";
        var allChats = await _chatRepository.GetAllSessionsAsync();
        var userChats = allChats.Where(c => c.UserId == userId || string.IsNullOrEmpty(c.UserId));
        RecentChats = new ObservableCollection<ChatSession>(
            userChats.OrderByDescending(c => c.StartedAt).Take(5));
    }

    [RelayCommand]
    private async Task Prayer()
    {
        await Shell.Current.GoToAsync("//prayer");
    }

    [RelayCommand]
    private async Task Bible()
    {
        await Shell.Current.GoToAsync("//BibleReader");
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
        await _navigationService.NavigateToAsync("//settings");
    }

    [RelayCommand]
    private async Task SwitchUser()
    {
        await _navigationService.NavigateToAsync("//userselection");
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
            await _navigationService.NavigateToAsync("//login");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to sign out: {ex.Message}");
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
