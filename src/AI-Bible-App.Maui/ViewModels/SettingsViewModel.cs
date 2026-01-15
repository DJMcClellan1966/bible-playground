using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Maui.Services;
using AI_Bible_App.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;
using System.Linq;

#pragma warning disable MVVMTK0045

namespace AI_Bible_App.Maui.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ITrainingDataExporter _exporter;
    private readonly IDialogService _dialogService;
    private readonly IUserService _userService;
    private readonly INavigationService _navigationService;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly IFontScaleService? _fontScaleService;
    private readonly AI_Bible_App.Core.Interfaces.INotificationService? _notificationService;
    private readonly IAuthenticationService _authService;
    private readonly IHealthCheckService? _healthCheckService;
    private readonly IBibleRAGService? _ragService;
    private readonly IConfiguration? _configuration;
    private readonly IAutonomousLearningService? _learningService;
    private readonly IUsageMetricsService? _usageMetrics;

    private static readonly string[] StorageDirectories =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-Bible-App"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoicesOfScripture")
    };

    private static readonly string RelativeDataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data");
    private static readonly string ChatDataPath = Path.Combine(RelativeDataDirectory, "chat_sessions.json");
    private static readonly string PrayerDataPath = Path.Combine(RelativeDataDirectory, "prayers.json");
    private static readonly string ReflectionDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AI-Bible-App",
        "reflections.json");
    private static readonly string BookmarkDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VoicesOfScripture",
        "verse_bookmarks.json");
    private static readonly string ExportDirectory = Path.Combine(FileSystem.AppDataDirectory, "exports");
    private static readonly string RagCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AI-Bible-App",
        "embeddings_cache.json");
    private static readonly string UsageMetricsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIBibleApp",
        "usage_metrics.json");

    private static readonly string[] DataRootCandidates =
    {
        Path.Combine(Directory.GetCurrentDirectory(), "data"),
        Path.Combine(FileSystem.AppDataDirectory, "data")
    };

    [ObservableProperty]
    private int totalSessions;

    [ObservableProperty]
    private int totalMessages;

    [ObservableProperty]
    private int ratedMessages;

    [ObservableProperty]
    private int positiveRatings;

    [ObservableProperty]
    private int negativeRatings;

    [ObservableProperty]
    private int messagesWithFeedback;

    // User Profile Properties
    [ObservableProperty]
    private string currentUserName = "Guest";

    [ObservableProperty]
    private string currentUserEmoji = "👤";

    [ObservableProperty]
    private DateTime currentUserSince = DateTime.UtcNow;

    [ObservableProperty]
    private bool isModerationEnabled = true;

    [ObservableProperty]
    private bool hasPinEnabled;

    [ObservableProperty]
    private string selectedTheme = "System";

    [ObservableProperty]
    private string selectedFontSize = "Medium";

    [ObservableProperty]
    private bool isDataSharingEnabled;

    [ObservableProperty]
    private AiBackendOption selectedAiBackend = new("auto", "Auto (local first, fallback to cloud)");

    // Cross-Device Sync Properties
    [ObservableProperty]
    private bool isSyncEnabled;

    [ObservableProperty]
    private string? syncCode;

    [ObservableProperty]
    private DateTime? lastSyncedAt;

    [ObservableProperty]
    private bool isSyncing;

    [ObservableProperty]
    private string syncStatusText = "Not synced";

    // Model Status Properties
    [ObservableProperty]
    private string modelStatusText = "Unknown";

    [ObservableProperty]
    private string modelStatusDetail = "Health check not run yet";

    [ObservableProperty]
    private DateTime? lastHealthCheckAt;

    [ObservableProperty]
    private string activeModelName = "Local (Ollama)";

    [ObservableProperty]
    private string aiBackendWarning = string.Empty;

    // Data Management Properties
    [ObservableProperty]
    private string localDataSize = "Calculating...";

    [ObservableProperty]
    private bool isClearingData;

    [ObservableProperty]
    private string dataClearStatus = string.Empty;

    [ObservableProperty]
    private string chatDataSize = "—";

    [ObservableProperty]
    private string prayerDataSize = "—";

    [ObservableProperty]
    private string reflectionDataSize = "—";

    [ObservableProperty]
    private string bookmarkDataSize = "—";

    [ObservableProperty]
    private string exportDataSize = "—";

    [ObservableProperty]
    private string ragCacheSize = "—";

    [ObservableProperty]
    private string usageMetricsSize = "—";

    // Learning & Research Properties
    [ObservableProperty]
    private string learningStatusText = "Not available";

    [ObservableProperty]
    private string learningModelVersion = "Unknown";

    [ObservableProperty]
    private string learningLastRun = "Never";

    [ObservableProperty]
    private string learningSummary = "No learning cycles yet";

    [ObservableProperty]
    private string researchWindowText = "Not configured";

    [ObservableProperty]
    private bool isAutonomousLearningEnabled;

    [ObservableProperty]
    private bool isAutonomousResearchEnabled;

    [ObservableProperty]
    private bool isContextualReferencesEnabled;

    // Usage Insights
    [ObservableProperty]
    private string usageSummary = "No usage data yet";

    [ObservableProperty]
    private string favoriteCharacter = "—";

    [ObservableProperty]
    private string mostSearchedBook = "—";

    [ObservableProperty]
    private string averageSessionText = "—";

    [ObservableProperty]
    private string daysSinceFirstUseText = "—";

    [ObservableProperty]
    private string topFeaturesText = "—";

    [ObservableProperty]
    private string lastActivityText = "—";

    [ObservableProperty]
    private int totalDevotionalsViewed;

    // Performance & Debug Properties
    [ObservableProperty]
    private bool isPerformanceModeEnabled;

    [ObservableProperty]
    private bool isRagDebugEnabled;

    [ObservableProperty]
    private string ragStatsSummary = "No RAG activity yet";

    // Daily Reminder Properties
    [ObservableProperty]
    private bool isDailyReminderEnabled;

    [ObservableProperty]
    private TimeSpan reminderTime = new TimeSpan(8, 0, 0);

    [ObservableProperty]
    private string reminderTimeDescription = "You'll receive a daily reminder at 8:00 AM";

    public List<string> ThemeOptions { get; } = new() { "System", "Light", "Dark" };
    public List<string> FontSizeOptions { get; } = new() { "Small", "Medium", "Large", "Extra Large" };
    public List<AiBackendOption> AiBackendOptions { get; } = new()
    {
        new AiBackendOption("auto", "Auto (local first, fallback to cloud)"),
        new AiBackendOption("local", "Local only (Ollama)"),
        new AiBackendOption("cloud", "Cloud preferred (Groq)")
    };

    public SettingsViewModel(
        ITrainingDataExporter exporter, 
        IDialogService dialogService, 
        IUserService userService, 
        INavigationService navigationService,
        ICloudSyncService cloudSyncService,
        IAuthenticationService authService,
        IFontScaleService? fontScaleService = null,
        AI_Bible_App.Core.Interfaces.INotificationService? notificationService = null,
        IHealthCheckService? healthCheckService = null,
        IBibleRAGService? ragService = null,
        IConfiguration? configuration = null,
        IAutonomousLearningService? learningService = null,
        IUsageMetricsService? usageMetrics = null)
    {
        _exporter = exporter;
        _dialogService = dialogService;
        _userService = userService;
        _navigationService = navigationService;
        _cloudSyncService = cloudSyncService;
        _authService = authService;
        _fontScaleService = fontScaleService;
        _notificationService = notificationService;
        _healthCheckService = healthCheckService;
        _ragService = ragService;
        _configuration = configuration;
        _learningService = learningService;
        _usageMetrics = usageMetrics;
        Title = "Settings";
        
        // Subscribe to user changes
        _userService.CurrentUserChanged += OnCurrentUserChanged;
        LoadUserProfile();
        
        // Load notification settings
        _ = LoadNotificationSettingsAsync();

        // Load preferences
        IsPerformanceModeEnabled = Preferences.Get("performance_mode_enabled", false);
        PerformanceSettings.IsEnabled = IsPerformanceModeEnabled;

        var defaultContextual = _configuration?["Features:ContextualReferences"]?.ToLower() == "true";
        IsContextualReferencesEnabled = Preferences.Get("contextual_refs_enabled", defaultContextual);
        IsRagDebugEnabled = Preferences.Get("rag_debug_enabled", false);

        LoadModelConfig();
        _ = RefreshModelStatusAsync();
        _ = RefreshStorageStatsAsync();
        _ = RefreshLearningStatsAsync();
        _ = RefreshUsageInsightsAsync();
    }

    private void OnCurrentUserChanged(object? sender, Core.Models.AppUser? user)
    {
        LoadUserProfile();
    }

    private void LoadUserProfile()
    {
        var user = _userService.CurrentUser;
        if (user != null)
        {
            CurrentUserName = user.Name;
            CurrentUserEmoji = user.AvatarEmoji ?? "👤";
            CurrentUserSince = user.CreatedAt;
            IsModerationEnabled = user.Settings.EnableContentModeration;
            IsDataSharingEnabled = user.Settings.ShareDataForImprovement;
            HasPinEnabled = user.HasPin;
            SelectedTheme = user.Settings.ThemePreference;
            SelectedFontSize = user.Settings.FontSizePreference;
            SelectedAiBackend = AiBackendOptions.FirstOrDefault(option =>
                string.Equals(option.Value, user.Settings.PreferredAIBackend, StringComparison.OrdinalIgnoreCase))
                ?? AiBackendOptions[0];
            UpdateActiveModelName();
            UpdateBackendWarning();
            
            // Load sync information
            IsSyncEnabled = user.HasSyncEnabled;
            if (user.SyncIdentity != null)
            {
                SyncCode = user.SyncIdentity.SyncCode;
                UpdateSyncStatus();
            }
            else
            {
                SyncCode = null;
                SyncStatusText = "Not set up";
            }
        }
        else
        {
            CurrentUserName = "Guest";
            CurrentUserEmoji = "👤";
            CurrentUserSince = DateTime.UtcNow;
            IsModerationEnabled = true;
            IsDataSharingEnabled = false;
            HasPinEnabled = false;
            SelectedTheme = "System";
            SelectedFontSize = "Medium";
            SelectedAiBackend = AiBackendOptions[0];
            UpdateActiveModelName();
            UpdateBackendWarning();
            IsSyncEnabled = false;
            SyncCode = null;
            SyncStatusText = "Not set up";
        }
    }

    private void UpdateSyncStatus()
    {
        var user = _userService.CurrentUser;
        if (user?.SyncIdentity != null)
        {
            // Check last sync from async call
            _ = UpdateSyncStatusAsync();
        }
    }

    private async Task UpdateSyncStatusAsync()
    {
        try
        {
            var user = _userService.CurrentUser;
            if (user == null) return;

            var status = await _cloudSyncService.GetSyncStatusAsync(user.Id);
            LastSyncedAt = status.LastSyncedAt;
            
            if (status.LastSyncedAt.HasValue)
            {
                var timeAgo = DateTime.UtcNow - status.LastSyncedAt.Value;
                if (timeAgo.TotalMinutes < 1)
                    SyncStatusText = "Just now";
                else if (timeAgo.TotalHours < 1)
                    SyncStatusText = $"{(int)timeAgo.TotalMinutes}m ago";
                else if (timeAgo.TotalDays < 1)
                    SyncStatusText = $"{(int)timeAgo.TotalHours}h ago";
                else
                    SyncStatusText = status.LastSyncedAt.Value.ToString("MMM d");
            }
            else
            {
                SyncStatusText = "Never synced";
            }
        }
        catch
        {
            SyncStatusText = "Unknown";
        }
    }

    partial void OnIsModerationEnabledChanged(bool value)
    {
        // Save the setting when toggled
        _ = SaveModerationSettingAsync(value);
    }

    partial void OnIsDataSharingEnabledChanged(bool value)
    {
        _ = SaveDataSharingSettingAsync(value);
    }

    partial void OnIsDailyReminderEnabledChanged(bool value)
    {
        // Save and update notification when toggled
        _ = SaveNotificationSettingAsync();
    }

    partial void OnIsPerformanceModeEnabledChanged(bool value)
    {
        Preferences.Set("performance_mode_enabled", value);
        PerformanceSettings.IsEnabled = value;
    }

    partial void OnIsRagDebugEnabledChanged(bool value)
    {
        Preferences.Set("rag_debug_enabled", value);
        RagStatsSummary = value ? BuildRagStatsSummary() : "RAG debug is disabled";
    }

    partial void OnReminderTimeChanged(TimeSpan value)
    {
        // Update description and save when time changes
        ReminderTimeDescription = $"You'll receive a daily reminder at {DateTime.Today.Add(value):h:mm tt}";
        if (IsDailyReminderEnabled)
        {
            _ = SaveNotificationSettingAsync();
        }
    }

    private async Task LoadNotificationSettingsAsync()
    {
        if (_notificationService == null) return;
        
        try
        {
            var settings = await _notificationService.GetSettingsAsync();
            IsDailyReminderEnabled = settings.DailyReminderEnabled;
            ReminderTime = new TimeSpan(settings.ReminderHour, settings.ReminderMinute, 0);
            ReminderTimeDescription = $"You'll receive a daily reminder at {DateTime.Today.Add(ReminderTime):h:mm tt}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Failed to load notification settings: {ex.Message}");
        }
    }

    private async Task SaveNotificationSettingAsync()
    {
        if (_notificationService == null) return;
        
        try
        {
            if (IsDailyReminderEnabled)
            {
                await _notificationService.ScheduleDailyReminderAsync(
                    ReminderTime.Hours,
                    ReminderTime.Minutes,
                    "Daily Devotional",
                    "Time for your daily Bible study and devotional!");
            }
            else
            {
                await _notificationService.CancelAllNotificationsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Failed to save notification setting: {ex.Message}");
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        // Save and apply theme when changed
        _ = SaveAndApplyThemeAsync(value);
    }

    partial void OnSelectedFontSizeChanged(string value)
    {
        // Save font size preference when changed
        _ = SaveFontSizeAsync(value);
    }

    partial void OnSelectedAiBackendChanged(AiBackendOption value)
    {
        _ = SavePreferredAiBackendAsync(value?.Value ?? "auto");
        UpdateActiveModelName();
        UpdateBackendWarning();
    }

    private async Task SaveAndApplyThemeAsync(string theme)
    {
        try
        {
            await _userService.UpdateCurrentUserAsync(user =>
            {
                user.Settings.ThemePreference = theme;
            });

            // Apply theme immediately
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current != null)
                {
                    Application.Current.UserAppTheme = theme switch
                    {
                        "Light" => AppTheme.Light,
                        "Dark" => AppTheme.Dark,
                        _ => AppTheme.Unspecified
                    };
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Failed to save theme setting: {ex.Message}");
        }
    }

    private async Task SaveFontSizeAsync(string fontSize)
    {
        try
        {
            await _userService.UpdateCurrentUserAsync(user =>
            {
                user.Settings.FontSizePreference = fontSize;
            });
            
            // Apply font scale immediately
            _fontScaleService?.ApplyScale(fontSize);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Failed to save font size setting: {ex.Message}");
        }
    }

    private async Task SavePreferredAiBackendAsync(string backend)
    {
        try
        {
            await _userService.UpdateCurrentUserAsync(user =>
            {
                user.Settings.PreferredAIBackend = backend;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Failed to save AI backend setting: {ex.Message}");
        }
    }

    private void UpdateActiveModelName()
    {
        var modelName = _configuration?["Ollama:ModelName"] ?? "phi4";
        var backend = SelectedAiBackend?.Value ?? "auto";

        ActiveModelName = backend switch
        {
            "local" => $"Local (Ollama) - {modelName}",
            "cloud" => "Cloud (Groq)",
            _ => $"Auto (local: {modelName}, cloud: Groq)"
        };
    }

    private void UpdateBackendWarning(bool? localHealthy = null)
    {
        var backend = SelectedAiBackend?.Value ?? "auto";
        var hasGroqKey = !string.IsNullOrWhiteSpace(_configuration?["Groq:ApiKey"]);

        AiBackendWarning = backend switch
        {
            "cloud" when !hasGroqKey => "Cloud selected but Groq API key is not configured.",
            "local" when localHealthy.HasValue && !localHealthy.Value => "Local model is offline. Start Ollama to use local mode.",
            "auto" when localHealthy.HasValue && !localHealthy.Value && !hasGroqKey => "Auto mode has no available backend. Start Ollama or configure Groq.",
            "auto" when localHealthy.HasValue && !localHealthy.Value => "Local is offline; auto mode will use cloud if configured.",
            _ => string.Empty
        };
    }

    private async Task SaveModerationSettingAsync(bool enabled)
    {
        try
        {
            await _userService.UpdateCurrentUserAsync(user =>
            {
                user.Settings.EnableContentModeration = enabled;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Failed to save moderation setting: {ex.Message}");
        }
    }

    private async Task SaveDataSharingSettingAsync(bool enabled)
    {
        try
        {
            await _userService.UpdateCurrentUserAsync(user =>
            {
                user.Settings.ShareDataForImprovement = enabled;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Failed to save data sharing setting: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SetOrChangePinAsync()
    {
        var user = _userService.CurrentUser;
        if (user == null) return;

        if (user.HasPin)
        {
            // Verify current PIN first
            var currentPin = await _dialogService.ShowPromptAsync(
                "Verify Current PIN",
                "Enter your current PIN:",
                maxLength: 6,
                keyboard: Keyboard.Numeric);
            
            if (string.IsNullOrEmpty(currentPin)) return;
            
            var isValid = await _userService.VerifyPinAsync(user.Id, currentPin);
            if (!isValid)
            {
                await _dialogService.ShowAlertAsync("Incorrect PIN", "The PIN you entered is incorrect.");
                return;
            }
        }

        // Get new PIN
        var newPin = await _dialogService.ShowPromptAsync(
            "Set New PIN",
            "Enter a 4-6 digit PIN to protect your profile:",
            maxLength: 6,
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrEmpty(newPin)) return;
        
        if (newPin.Length < 4)
        {
            await _dialogService.ShowAlertAsync("PIN Too Short", "PIN must be at least 4 digits.");
            return;
        }

        // Confirm PIN
        var confirmPin = await _dialogService.ShowPromptAsync(
            "Confirm PIN",
            "Enter your PIN again to confirm:",
            maxLength: 6,
            keyboard: Keyboard.Numeric);
        
        if (newPin != confirmPin)
        {
            await _dialogService.ShowAlertAsync("PINs Don't Match", "The PINs you entered don't match. Please try again.");
            return;
        }

        try
        {
            await _userService.SetUserPinAsync(user.Id, newPin);
            HasPinEnabled = true;
            await _dialogService.ShowAlertAsync("PIN Set", "Your PIN has been set successfully. You'll need to enter it when switching to your profile.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to set PIN: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemovePinAsync()
    {
        var user = _userService.CurrentUser;
        if (user == null || !user.HasPin) return;

        // Verify current PIN
        var currentPin = await _dialogService.ShowPromptAsync(
            "Verify PIN",
            "Enter your current PIN to remove protection:",
            maxLength: 6,
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrEmpty(currentPin)) return;
        
        var isValid = await _userService.VerifyPinAsync(user.Id, currentPin);
        if (!isValid)
        {
            await _dialogService.ShowAlertAsync("Incorrect PIN", "The PIN you entered is incorrect.");
            return;
        }

        var confirm = await _dialogService.ShowConfirmAsync(
            "Remove PIN",
            "Are you sure you want to remove PIN protection? Anyone will be able to access your profile.",
            "Remove", "Cancel");
        
        if (!confirm) return;

        try
        {
            await _userService.RemovePinAsync(user.Id);
            HasPinEnabled = false;
            await _dialogService.ShowAlertAsync("PIN Removed", "PIN protection has been removed from your profile.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to remove PIN: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SwitchUserAsync()
    {
        await Shell.Current.GoToAsync("//userselection");
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var confirm = await _dialogService.ShowConfirmAsync(
            "Sign Out",
            "Are you sure you want to sign out? You'll need to sign in again to access your data.");
        
        if (confirm)
        {
            try
            {
                // Clear all saved preferences related to the session
                Preferences.Remove("stay_logged_in");
                Preferences.Remove("onboarding_profile");
                
                await _authService.SignOutAsync();
                await Shell.Current.GoToAsync("//login");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to sign out: {ex.Message}");
            }
        }
    }

    public async Task InitializeAsync()
    {
        _usageMetrics?.TrackFeatureUsed("Settings");
        LoadUserProfile();
        await RefreshStatsAsync();
        await RefreshModelStatusAsync();
        await RefreshStorageStatsAsync();
        await RefreshLearningStatsAsync();
        await RefreshUsageInsightsAsync();
        RagStatsSummary = IsRagDebugEnabled ? BuildRagStatsSummary() : "RAG debug is disabled";
    }

    partial void OnIsAutonomousLearningEnabledChanged(bool value)
    {
        Preferences.Set("autonomous_learning_enabled", value);
    }

    partial void OnIsAutonomousResearchEnabledChanged(bool value)
    {
        Preferences.Set("autonomous_research_enabled", value);
    }

    partial void OnIsContextualReferencesEnabledChanged(bool value)
    {
        Preferences.Set("contextual_refs_enabled", value);
    }

    private void LoadModelConfig()
    {
        if (_configuration == null)
            return;

        var modelName = _configuration["Ollama:ModelName"] ?? "phi4";
        ActiveModelName = $"Local (Ollama) - {modelName}";
    }

    [RelayCommand]
    private async Task RefreshModelStatusAsync()
    {
        if (_healthCheckService == null)
        {
            ModelStatusText = "Unavailable";
            ModelStatusDetail = "Health check service not configured";
            LastHealthCheckAt = DateTime.UtcNow;
            return;
        }

        try
        {
            var status = await _healthCheckService.GetHealthStatusAsync();
            LastHealthCheckAt = DateTime.UtcNow;
            ModelStatusText = status.IsHealthy ? "Online" : "Offline";
            ModelStatusDetail = status.IsHealthy
                ? "Local model is reachable"
                : status.ErrorMessage ?? "Local model is not reachable";
            UpdateBackendWarning(status.IsHealthy);
        }
        catch (Exception ex)
        {
            LastHealthCheckAt = DateTime.UtcNow;
            ModelStatusText = "Unknown";
            ModelStatusDetail = $"Health check failed: {ex.Message}";
            UpdateBackendWarning(null);
        }
    }

    [RelayCommand]
    private async Task RefreshStorageStatsAsync()
    {
        try
        {
            var bytes = await Task.Run(() => StorageDirectories.Sum(GetDirectorySize));
            LocalDataSize = FormatBytes(bytes);

            ChatDataSize = FormatBytes(GetFileSize(ChatDataPath));
            PrayerDataSize = FormatBytes(GetFileSize(PrayerDataPath));
            ReflectionDataSize = FormatBytes(GetFileSize(ReflectionDataPath));
            BookmarkDataSize = FormatBytes(GetFileSize(BookmarkDataPath));
            ExportDataSize = FormatBytes(GetDirectorySize(ExportDirectory));
            RagCacheSize = FormatBytes(GetFileSize(RagCachePath));
            UsageMetricsSize = FormatBytes(GetFileSize(UsageMetricsPath));
        }
        catch
        {
            LocalDataSize = "Unknown";
        }
    }

    [RelayCommand]
    private async Task ClearLocalDataAsync()
    {
        var confirm = await _dialogService.ShowConfirmAsync(
            "Clear Local Data",
            "This will remove chats, prayers, reflections, bookmarks, and cached files stored on this device. Continue?",
            "Clear", "Cancel");

        if (!confirm)
            return;

        try
        {
            IsClearingData = true;
            await Task.Run(() =>
            {
                foreach (var dir in StorageDirectories)
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }

                foreach (var dir in DataRootCandidates)
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }
            });

            await _dialogService.ShowAlertAsync("Cleared", "Local data has been removed.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to clear data: {ex.Message}");
        }
        finally
        {
            IsClearingData = false;
            await RefreshStorageStatsAsync();
        }
    }


    [RelayCommand]
    private async Task ClearExportsAsync()
    {
        var confirm = await _dialogService.ShowConfirmAsync(
            "Clear Exports",
            "This will remove all exported training data files on this device. Continue?",
            "Clear", "Cancel");

        if (!confirm)
            return;

        try
        {
            IsClearingData = true;
            if (Directory.Exists(ExportDirectory))
            {
                Directory.Delete(ExportDirectory, recursive: true);
            }
            DataClearStatus = "Exported files removed.";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to clear exports: {ex.Message}");
        }
        finally
        {
            IsClearingData = false;
            await RefreshStorageStatsAsync();
        }
    }

    [RelayCommand]
    private async Task ClearUsageMetricsAsync()
    {
        if (_usageMetrics == null)
        {
            await _dialogService.ShowAlertAsync("Not Available", "Usage metrics are not available on this device.");
            return;
        }

        var confirm = await _dialogService.ShowConfirmAsync(
            "Clear Usage Metrics",
            "This will reset local usage stats and remove the metrics file on this device. Continue?",
            "Clear", "Cancel");

        if (!confirm)
            return;

        try
        {
            IsClearingData = true;
            _usageMetrics.ResetMetrics();

            if (File.Exists(UsageMetricsPath))
            {
                File.Delete(UsageMetricsPath);
            }

            DataClearStatus = "Usage metrics cleared.";
            await RefreshUsageInsightsAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to clear usage metrics: {ex.Message}");
        }
        finally
        {
            IsClearingData = false;
            await RefreshStorageStatsAsync();
        }
    }

    [RelayCommand]
    private async Task ExportChatsAsync()
    {
        await ExportLocalFileAsync(ChatDataPath, "Chat history", "chats");
    }

    [RelayCommand]
    private async Task ExportPrayersAsync()
    {
        await ExportLocalFileAsync(PrayerDataPath, "Prayers", "prayers");
    }

    [RelayCommand]
    private async Task ExportReflectionsAsync()
    {
        await ExportLocalFileAsync(ReflectionDataPath, "Reflections", "reflections");
    }

    [RelayCommand]
    private async Task ExportBookmarksAsync()
    {
        await ExportLocalFileAsync(BookmarkDataPath, "Bookmarks", "bookmarks");
    }

    private async Task ExportLocalFileAsync(string sourcePath, string label, string filePrefix)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                await _dialogService.ShowAlertAsync("No Data", $"{label} file not found on this device.");
                return;
            }

            Directory.CreateDirectory(ExportDirectory);
            var fileName = $"{filePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var destination = Path.Combine(ExportDirectory, fileName);
            File.Copy(sourcePath, destination, overwrite: true);

            await _dialogService.ShowAlertAsync(
                "Export Complete",
                $"Exported {label} to:\n{destination}");

            await ShareFileAsync(destination);
            await RefreshStorageStatsAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Export Failed", ex.Message);
        }
    }


    [RelayCommand]
    private async Task ClearChatHistoryAsync()
    {
        await ClearDataFilesAsync("Clear Chat History",
            "This removes all saved chat sessions on this device.",
            GetDataFileCandidates("chat_sessions.json"));
    }

    [RelayCommand]
    private async Task ClearPrayerHistoryAsync()
    {
        await ClearDataFilesAsync("Clear Prayer History",
            "This removes all saved prayers on this device.",
            GetDataFileCandidates("prayers.json"));
    }

    [RelayCommand]
    private async Task ClearReflectionsAsync()
    {
        var file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App",
            "reflections.json");

        await ClearDataFilesAsync("Clear Reflections",
            "This removes all saved reflections on this device.",
            new[] { file });
    }

    [RelayCommand]
    private async Task ClearBookmarksAsync()
    {
        var file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoicesOfScripture",
            "verse_bookmarks.json");

        await ClearDataFilesAsync("Clear Bookmarks",
            "This removes all saved verse bookmarks on this device.",
            new[] { file });
    }

    [RelayCommand]
    private async Task ClearRagCacheAsync()
    {
        var file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App",
            "embeddings_cache.json");

        await ClearDataFilesAsync("Clear RAG Cache",
            "This clears cached embeddings used for RAG.",
            new[] { file });
    }

    private async Task ClearDataFilesAsync(string title, string message, IEnumerable<string> files)
    {
        var confirm = await _dialogService.ShowConfirmAsync(title, $"{message}\n\nContinue?", "Clear", "Cancel");
        if (!confirm)
            return;

        try
        {
            IsClearingData = true;
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    TryDeleteFile(file);
                }
            });

            DataClearStatus = $"{title} complete";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to clear data: {ex.Message}");
            DataClearStatus = $"{title} failed";
        }
        finally
        {
            IsClearingData = false;
            await RefreshStorageStatsAsync();
        }
    }

    private static IEnumerable<string> GetDataFileCandidates(string fileName)
    {
        foreach (var dir in DataRootCandidates)
        {
            yield return Path.Combine(dir, fileName);
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore deletion failures for individual files
        }
    }

    [RelayCommand]
    private void RefreshRagStats()
    {
        RagStatsSummary = BuildRagStatsSummary();
    }

    private string BuildRagStatsSummary()
    {
        if (_ragService == null)
            return "RAG service not configured";

        var stats = _ragService.LastSearchStats;
        if (string.IsNullOrWhiteSpace(stats.Query))
            return "No RAG activity yet";

        var durationMs = (int)stats.SearchDuration.TotalMilliseconds;
        return $"Last query: \"{stats.Query}\" | Results: {stats.TotalResults} | " +
               $"Semantic: {stats.SemanticResults} | Keyword: {stats.KeywordFallbackResults} | " +
               $"Duration: {durationMs}ms";
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long size = 0;
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                size += new FileInfo(file).Length;
            }
            catch
            {
                // Ignore unreadable files
            }
        }
        return size;
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var order = 0;
        while (value >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            value /= 1024;
        }
        return $"{value:0.##} {suffixes[order]}";
    }

    [RelayCommand]
    private async Task RefreshLearningStatsAsync()
    {
        if (_learningService == null)
        {
            LearningStatusText = "Not available";
            return;
        }

        try
        {
            var stats = await _learningService.GetLearningStatisticsAsync();
            LearningStatusText = stats.TotalLearningCycles > 0 ? "Active" : "Idle";
            LearningModelVersion = stats.CurrentModelVersion;
            LearningLastRun = stats.LastLearningCycle?.ToString("MMM d, h:mm tt") ?? "Never";
            LearningSummary = $"Cycles: {stats.TotalLearningCycles} | Deployments: {stats.SuccessfulDeployments}";
        }
        catch (Exception ex)
        {
            LearningStatusText = "Error";
            LearningSummary = ex.Message;
        }

        if (_configuration != null)
        {
            var enabled = _configuration["AutonomousResearch:Enabled"] == "true";
            var startHour = int.TryParse(_configuration["AutonomousResearch:StartHour"], out var sh) ? sh : 2;
            var endHour = int.TryParse(_configuration["AutonomousResearch:EndHour"], out var eh) ? eh : 6;
            ResearchWindowText = enabled ? $"{startHour:00}:00–{endHour:00}:00" : "Disabled";
        }

        IsAutonomousLearningEnabled = Preferences.Get("autonomous_learning_enabled", true);
        var researchPreference = Preferences.Get("autonomous_research_enabled", true);
        var configResearchEnabled = _configuration?["AutonomousResearch:Enabled"] == "true";
        IsAutonomousResearchEnabled = researchPreference && configResearchEnabled;
    }

    [RelayCommand]
    private Task RefreshUsageInsightsAsync()
    {
        if (_usageMetrics == null)
        {
            UsageSummary = "Usage metrics unavailable";
            return Task.CompletedTask;
        }

        var insights = _usageMetrics.GetInsights();
        var metrics = _usageMetrics.GetMetrics();
        UsageSummary = $"Conversations: {insights.TotalConversations} | Prayers: {insights.TotalPrayers} | " +
                       $"Searches: {insights.TotalBibleSearches} | Devotionals: {insights.TotalDevotionalsViewed}";
        FavoriteCharacter = insights.FavoriteCharacter != null
            ? $"{insights.FavoriteCharacter} ({insights.FavoriteCharacterCount})"
            : "—";
        MostSearchedBook = insights.MostSearchedBook != null
            ? $"{insights.MostSearchedBook} ({insights.MostSearchedBookCount})"
            : "—";
        TotalDevotionalsViewed = insights.TotalDevotionalsViewed;
        AverageSessionText = insights.TotalSessions > 0
            ? $"{insights.AverageSessionMinutes} min"
            : "—";
        DaysSinceFirstUseText = insights.DaysSinceFirstUse > 0
            ? $"{insights.DaysSinceFirstUse} days"
            : "—";
        TopFeaturesText = BuildTopFeaturesText();
        LastActivityText = FormatLastActivity(metrics.LastActivityDate);

        return Task.CompletedTask;
    }

    private string BuildTopFeaturesText()
    {
        if (_usageMetrics == null)
            return "—";

        var metrics = _usageMetrics.GetMetrics();
        if (metrics.FeatureUsage.Count == 0)
            return "—";

        return string.Join(" | ", metrics.FeatureUsage
            .OrderByDescending(pair => pair.Value)
            .Take(3)
            .Select(pair => $"{MapFeatureName(pair.Key)} ({pair.Value})"));
    }

    private static string FormatLastActivity(DateTime? lastActivity)
    {
        if (!lastActivity.HasValue)
            return "Never";

        return lastActivity.Value.ToLocalTime().ToString("MMM d, h:mm tt");
    }

    private static string MapFeatureName(string key)
    {
        return key switch
        {
            "Home" => "Home",
            "Chat" => "Chat",
            "PrayerGenerator" => "Prayer",
            "BibleReader" => "Bible Reader",
            "Devotional" => "Devotional",
            "Bookmarks" => "Bookmarks",
            "Reflections" => "Reflections",
            "ChatHistory" => "Chat History",
            "CharacterSelection" => "Character Selection",
            "MultiCharacterSelection" => "Multi-Character",
            "Roundtable" => "Roundtable",
            "WisdomCouncil" => "Wisdom Council",
            "PrayerChain" => "Prayer Chain",
            "ReadingPlans" => "Reading Plans",
            "CustomCharacters" => "Custom Characters",
            "CharacterEvolution" => "Character Growth",
            "HistoryDashboard" => "My Journey",
            "OfflineModels" => "Offline Models",
            "SystemDiagnostics" => "Diagnostics",
            "Settings" => "Settings",
            "Initialization" => "Startup",
            "Admin" => "Admin",
            _ => key
        };
    }

    [RelayCommand]
    private async Task ResetUsageMetricsAsync()
    {
        if (_usageMetrics == null)
            return;

        var confirm = await _dialogService.ShowConfirmAsync(
            "Reset Usage Metrics",
            "This will clear local usage stats (conversations, prayers, searches). Continue?",
            "Reset", "Cancel");

        if (!confirm)
            return;

        _usageMetrics.ResetMetrics();
        await RefreshUsageInsightsAsync();
    }

    [RelayCommand]
    private async Task ExportUsageMetricsAsync()
    {
        if (_usageMetrics == null)
            return;

        try
        {
            Directory.CreateDirectory(ExportDirectory);
            var fileName = $"usage_metrics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var destination = Path.Combine(ExportDirectory, fileName);
            File.WriteAllText(destination, _usageMetrics.ExportMetrics());

            await _dialogService.ShowAlertAsync(
                "Export Complete",
                $"Exported usage metrics to:\n{destination}");

            await ShareFileAsync(destination);
            await RefreshStorageStatsAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Export Failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RefreshStatsAsync()
    {
        try
        {
            IsBusy = true;
            var stats = await _exporter.GetStatsAsync();
            
            TotalSessions = stats.TotalSessions;
            TotalMessages = stats.TotalMessages;
            RatedMessages = stats.RatedMessages;
            PositiveRatings = stats.PositiveRatings;
            NegativeRatings = stats.NegativeRatings;
            MessagesWithFeedback = stats.MessagesWithFeedback;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAllRatedAsync()
    {
        try
        {
            IsBusy = true;
            
            if (RatedMessages == 0)
            {
                await _dialogService.ShowAlertAsync("No Data", "No rated messages to export. Rate some AI responses first!");
                return;
            }

            var filePath = await _exporter.ExportToJsonlAsync(onlyPositiveRatings: false);
            
            await _dialogService.ShowAlertAsync(
                "Export Complete",
                $"Exported {RatedMessages} rated conversations to:\n{filePath}");
                
            // Optionally share the file
            await ShareFileAsync(filePath);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Export Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportPositiveOnlyAsync()
    {
        try
        {
            IsBusy = true;
            
            if (PositiveRatings == 0)
            {
                await _dialogService.ShowAlertAsync("No Data", "No positive ratings to export. Give some thumbs up first!");
                return;
            }

            var filePath = await _exporter.ExportToJsonlAsync(onlyPositiveRatings: true);
            
            await _dialogService.ShowAlertAsync(
                "Export Complete",
                $"Exported {PositiveRatings} positive-rated conversations to:\n{filePath}");
                
            await ShareFileAsync(filePath);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Export Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShareFileAsync(string filePath)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Training Data",
                File = new ShareFile(filePath)
            });
        }
        catch
        {
            // Sharing not available on all platforms
        }
    }

    [RelayCommand]
    private async Task NavigateToOfflineModelsAsync()
    {
        await _navigationService.NavigateToAsync("OfflineModelsPage");
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // CROSS-DEVICE SYNC COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateSyncCodeAsync()
    {
        var user = _userService.CurrentUser;
        if (user == null)
        {
            await _dialogService.ShowAlertAsync("Error", "Please select a user first.");
            return;
        }

        try
        {
            IsSyncing = true;
            SyncStatusText = "Generating code...";

            var result = await _cloudSyncService.GenerateSyncCodeAsync(user.Id);
            
            if (result.Success && result.SyncCode != null)
            {
                // Refresh the user to get updated SyncIdentity (set by the service)
                LoadUserProfile();
                
                SyncCode = result.SyncCode;
                IsSyncEnabled = true;
                SyncStatusText = "Ready to sync";

                await _dialogService.ShowAlertAsync(
                    "☁️ Sync Code Generated",
                    $"Your sync code is:\n\n{result.SyncCode}\n\n" +
                    "Enter this code on your other devices to sync your data.\n\n" +
                    "💡 Tip: This code links all your devices together.",
                    "Got it!");
            }
            else
            {
                await _dialogService.ShowAlertAsync("Error", result.ErrorMessage ?? "Failed to generate sync code.");
                SyncStatusText = "Error";
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to generate sync code: {ex.Message}");
            SyncStatusText = "Error";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task LinkDeviceAsync()
    {
        var user = _userService.CurrentUser;
        if (user == null)
        {
            await _dialogService.ShowAlertAsync("Error", "Please select a user first.");
            return;
        }

        // Prompt for sync code from another device
        var syncCode = await _dialogService.ShowPromptAsync(
            "🔗 Link Device",
            "Enter the sync code from your other device:\n\n(Example: FAITH-7X3K-HOPE)",
            maxLength: 20);

        if (string.IsNullOrWhiteSpace(syncCode))
            return;

        // Normalize the code (uppercase, handle missing dashes)
        syncCode = syncCode.Trim().ToUpperInvariant();

        try
        {
            IsSyncing = true;
            SyncStatusText = "Linking device...";

            var result = await _cloudSyncService.LinkWithSyncCodeAsync(syncCode);
            
            if (result.Success)
            {
                // Refresh user to get updated sync identity
                LoadUserProfile();
                
                SyncCode = syncCode;
                IsSyncEnabled = true;
                
                // Ask if user wants to sync now
                var syncNow = await _dialogService.ShowConfirmAsync(
                    "✅ Device Linked",
                    $"Your device has been linked successfully!\n\n" +
                    $"Synced {result.ItemsSynced} items from {result.UserName ?? "your account"}.\n\n" +
                    "Would you like to sync your data now?",
                    "Sync Now", "Later");

                if (syncNow)
                {
                    await PerformSyncAsync();
                }
                else
                {
                    SyncStatusText = "Linked";
                }
            }
            else
            {
                await _dialogService.ShowAlertAsync("Link Failed", result.ErrorMessage ?? "Could not link with that sync code.");
                SyncStatusText = "Link failed";
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to link device: {ex.Message}");
            SyncStatusText = "Error";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        var user = _userService.CurrentUser;
        if (user == null || !user.HasSyncEnabled)
        {
            await _dialogService.ShowAlertAsync(
                "Sync Not Set Up",
                "Please generate a sync code or link your device first.");
            return;
        }

        await PerformSyncAsync();
    }

    private async Task PerformSyncAsync()
    {
        var user = _userService.CurrentUser;
        if (user == null) return;

        try
        {
            IsSyncing = true;
            SyncStatusText = "Syncing...";

            var result = await _cloudSyncService.FullSyncAsync(user.Id);
            
            if (result.Success)
            {
                LastSyncedAt = DateTime.UtcNow;
                
                var changes = new List<string>();
                if (result.ItemsUploaded > 0)
                    changes.Add($"{result.ItemsUploaded} uploaded");
                if (result.ItemsDownloaded > 0)
                    changes.Add($"{result.ItemsDownloaded} downloaded");
                
                var summary = changes.Count > 0 
                    ? string.Join(", ", changes)
                    : "Everything up to date";

                SyncStatusText = "Just now";
                
                await _dialogService.ShowAlertAsync(
                    "✅ Sync Complete",
                    $"{summary}\n\nYour data is now synchronized across your devices.");

                // Reload profile to reflect any downloaded changes
                LoadUserProfile();
            }
            else
            {
                SyncStatusText = "Sync failed";
                await _dialogService.ShowAlertAsync("Sync Failed", result.ErrorMessage ?? "Unknown error during sync.");
            }
        }
        catch (Exception ex)
        {
            SyncStatusText = "Sync error";
            await _dialogService.ShowAlertAsync("Error", $"Sync failed: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task RemoveSyncAsync()
    {
        var user = _userService.CurrentUser;
        if (user == null || !user.HasSyncEnabled) return;

        var confirm = await _dialogService.ShowConfirmAsync(
            "Remove Sync",
            "This will unlink this device from cloud sync. Your local data will remain, but won't sync to other devices.\n\nContinue?",
            "Remove Sync", "Cancel");

        if (!confirm) return;

        try
        {
            user.SyncIdentity = null;
            await _userService.UpdateCurrentUserAsync(u => u.SyncIdentity = null);
            
            IsSyncEnabled = false;
            SyncCode = null;
            LastSyncedAt = null;
            SyncStatusText = "Not set up";
            
            await _dialogService.ShowAlertAsync("Sync Removed", "This device has been unlinked from cloud sync.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to remove sync: {ex.Message}");
        }
    }
}

public record AiBackendOption(string Value, string Label);
