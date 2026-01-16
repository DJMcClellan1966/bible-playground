using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045 // AOT compatibility warning for WinRT scenarios

namespace AI_Bible_App.Maui.ViewModels;

public partial class PrayerViewModel : BaseViewModel
{
    private readonly IAIService _aiService;
    private readonly IPrayerRepository _prayerRepository;
    private readonly IReflectionRepository _reflectionRepository;
    private readonly IDialogService _dialogService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private string prayerRequest = string.Empty;

    [ObservableProperty]
    private int prayerRequestLength;

    [ObservableProperty]
    private int prayerRequestRemaining = 1000;

    [ObservableProperty]
    private string generatedPrayer = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Prayer> savedPrayers = new();

    [ObservableProperty]
    private Prayer? selectedPrayer;

    [ObservableProperty]
    private bool isGenerating;

    // Personalization options
    [ObservableProperty]
    private bool showAdvancedOptions;

    [ObservableProperty]
    private PrayerMood? selectedMood;

    [ObservableProperty]
    private PrayerRequestType selectedStyle = PrayerRequestType.General;

    [ObservableProperty]
    private PrayerLength selectedLength = PrayerLength.Medium;

    [ObservableProperty]
    private PrayerTradition selectedTradition = PrayerTradition.General;

    [ObservableProperty]
    private TimeOfDayContext? selectedTimeContext;

    [ObservableProperty]
    private string? prayingFor;

    [ObservableProperty]
    private string? lifeCircumstances;

    [ObservableProperty]
    private bool includeScripture = true;

    // Option lists for pickers
    public List<PrayerMood> AvailableMoods => Enum.GetValues<PrayerMood>().ToList();
    public List<PrayerRequestType> AvailableStyles => Enum.GetValues<PrayerRequestType>().ToList();
    public List<PrayerLength> AvailableLengths => Enum.GetValues<PrayerLength>().ToList();
    public List<PrayerTradition> AvailableTraditions => Enum.GetValues<PrayerTradition>().ToList();
    public List<TimeOfDayContext> AvailableTimeContexts => Enum.GetValues<TimeOfDayContext>().ToList();
    private static readonly string[] DefaultQuickPrompts =
    [
        "Peace and anxiety relief",
        "Guidance for a big decision",
        "Strength in hardship",
        "Healing for a loved one",
        "Gratitude and thanksgiving",
        "Forgiveness and renewal"
    ];

    public ObservableCollection<string> QuickPrompts { get; } = new(DefaultQuickPrompts);

    public PrayerViewModel(
        IAIService aiService,
        IPrayerRepository prayerRepository,
        IReflectionRepository reflectionRepository,
        IDialogService dialogService,
        IUsageMetricsService? usageMetrics = null)
    {
        _aiService = aiService;
        _prayerRepository = prayerRepository;
        _reflectionRepository = reflectionRepository;
        _dialogService = dialogService;
        _usageMetrics = usageMetrics;
        Title = "Prayer Generator";
        
        // Auto-detect time context
        var hour = DateTime.Now.Hour;
        SelectedTimeContext = hour switch
        {
            >= 5 and < 12 => TimeOfDayContext.Morning,
            >= 12 and < 17 => TimeOfDayContext.Midday,
            >= 17 and < 21 => TimeOfDayContext.Evening,
            _ => TimeOfDayContext.Night
        };
    }

    partial void OnPrayerRequestChanged(string value)
    {
        var length = value?.Length ?? 0;
        PrayerRequestLength = length;
        PrayerRequestRemaining = Math.Max(0, 1000 - length);
    }

    partial void OnSelectedPrayerChanged(Prayer? value)
    {
        if (value != null)
        {
            _ = HandlePrayerSelectedAsync(value);
        }
    }

    private async Task HandlePrayerSelectedAsync(Prayer prayer)
    {
        await ViewPrayer(prayer);
        SelectedPrayer = null; // Clear selection for next time
    }
    
    public async Task InitializeAsync()
    {
        _usageMetrics?.TrackFeatureUsed("PrayerGenerator");
        EnsureQuickPrompts();
        await LoadSavedPrayersAsync();
    }

    private void EnsureQuickPrompts()
    {
        if (QuickPrompts.Count > 0)
            return;

        foreach (var prompt in DefaultQuickPrompts)
        {
            QuickPrompts.Add(prompt);
        }
    }

    private async Task LoadSavedPrayersAsync()
    {
        try
        {
            var prayers = await _prayerRepository.GetAllPrayersAsync();
            SavedPrayers = new ObservableCollection<Prayer>(prayers.OrderByDescending(p => p.CreatedAt));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"Failed to load prayers: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleAdvancedOptions()
    {
        ShowAdvancedOptions = !ShowAdvancedOptions;
    }

    [RelayCommand]
    private void UsePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        PrayerRequest = prompt;
    }

    [RelayCommand]
    private async Task GeneratePrayer()
    {
        if (string.IsNullOrWhiteSpace(PrayerRequest) || IsGenerating)
            return;

        try
        {
            IsGenerating = true;
            GeneratedPrayer = "Generating prayer...";

            string prayer;
            
            // Use personalized prayer if any advanced options are set
            if (ShowAdvancedOptions || SelectedMood.HasValue || SelectedStyle != PrayerRequestType.General)
            {
                var options = new PrayerOptions
                {
                    Topic = PrayerRequest,
                    Mood = SelectedMood,
                    RequestType = SelectedStyle,
                    Length = SelectedLength,
                    Tradition = SelectedTradition,
                    TimeContext = SelectedTimeContext,
                    PrayingFor = PrayingFor,
                    LifeCircumstances = LifeCircumstances,
                    IncludeScripture = IncludeScripture
                };
                
                prayer = await _aiService.GeneratePersonalizedPrayerAsync(options);
            }
            else
            {
                prayer = await _aiService.GeneratePrayerAsync(PrayerRequest);
            }
            
            GeneratedPrayer = prayer;
            _usageMetrics?.TrackPrayerGenerated(PrayerRequest, SelectedMood?.ToString());
        }
        catch (Exception ex)
        {
            GeneratedPrayer = string.Empty;
            await _dialogService.ShowAlertAsync("Error", ex.Message);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task SavePrayer()
    {
        if (string.IsNullOrWhiteSpace(GeneratedPrayer))
            return;

        try
        {
            var prayer = new Prayer
            {
                Id = Guid.NewGuid().ToString(),
                Topic = PrayerRequest,
                Content = GeneratedPrayer,
                CreatedAt = DateTime.UtcNow
            };

            await _prayerRepository.SavePrayerAsync(prayer);
            SavedPrayers.Insert(0, prayer);

            await _dialogService.ShowAlertAsync("Success", "Prayer saved successfully!");

            // Clear for new prayer
            PrayerRequest = string.Empty;
            GeneratedPrayer = string.Empty;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ViewPrayer(Prayer prayer)
    {
        if (prayer == null)
            return;

        // Convert UTC to local time for display
        var localTime = prayer.CreatedAt.Kind == DateTimeKind.Utc 
            ? prayer.CreatedAt.ToLocalTime() 
            : DateTime.SpecifyKind(prayer.CreatedAt, DateTimeKind.Utc).ToLocalTime();
        
        await _dialogService.ShowAlertAsync(
            $"Prayer: {prayer.Topic}",
            $"{prayer.Content}\n\n— Saved {localTime:MMMM d, yyyy h:mm tt}",
            "Close");
    }

    [RelayCommand]
    private void NewPrayer()
    {
        PrayerRequest = string.Empty;
        GeneratedPrayer = string.Empty;
    }

    [RelayCommand]
    private async Task CopyPrayer()
    {
        if (string.IsNullOrWhiteSpace(GeneratedPrayer))
            return;

        await Clipboard.Default.SetTextAsync(GeneratedPrayer);
        await _dialogService.ShowAlertAsync("Copied", "Prayer copied to clipboard.");
    }

    [RelayCommand]
    private async Task SaveToReflections()
    {
        if (string.IsNullOrWhiteSpace(GeneratedPrayer)) return;

        try
        {
            var title = await _dialogService.ShowPromptAsync(
                "Save to Reflections",
                "Give this reflection a title:",
                initialValue: $"Prayer: {(string.IsNullOrEmpty(PrayerRequest) ? "Daily Prayer" : PrayerRequest)}",
                maxLength: 100);

            if (title == null) return; // Cancelled

            var reflection = new Reflection
            {
                Title = title,
                SavedContent = GeneratedPrayer,
                Type = ReflectionType.Prayer,
                CreatedAt = DateTime.UtcNow
            };

            await _reflectionRepository.SaveReflectionAsync(reflection);

            var goToReflections = await _dialogService.ShowConfirmAsync(
                "Saved! ✓",
                "This prayer has been saved to your reflections. Would you like to add your thoughts now?",
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
    }
}
