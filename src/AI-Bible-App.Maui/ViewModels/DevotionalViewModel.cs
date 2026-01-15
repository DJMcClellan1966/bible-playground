using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045

namespace AI_Bible_App.Maui.ViewModels;

public partial class DevotionalViewModel : BaseViewModel
{
    private readonly IDevotionalRepository _devotionalRepository;
    private readonly IDialogService _dialogService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private Devotional? todaysDevotional;

    [ObservableProperty]
    private ObservableCollection<Devotional> recentDevotionals = new();

    [ObservableProperty]
    private bool isGenerating;

    [ObservableProperty]
    private bool showHistory;

    [ObservableProperty]
    private string generatingMessage = "Generating today's devotional...";

    public DevotionalViewModel(
        IDevotionalRepository devotionalRepository,
        IDialogService dialogService,
        IUsageMetricsService? usageMetrics = null)
    {
        _devotionalRepository = devotionalRepository;
        _dialogService = dialogService;
        _usageMetrics = usageMetrics;
        Title = "Daily Devotional";
    }

    public async Task InitializeAsync()
    {
        _usageMetrics?.TrackFeatureUsed("Devotional");
        await LoadTodaysDevotionalAsync();
        await LoadRecentDevotionalsAsync();
    }

    [RelayCommand]
    private async Task LoadTodaysDevotionalAsync()
    {
        try
        {
            IsBusy = true;
            
            // Try to get today's devotional
            var today = DateTime.Today;
            TodaysDevotional = await _devotionalRepository.GetDevotionalForDateAsync(today);
            
            // If none exists, generate one
            if (TodaysDevotional == null)
            {
                await GenerateDevotionalAsync();
            }
            else
            {
                _usageMetrics?.TrackDevotionalViewed();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Devotional] Error loading: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to load today's devotional.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateDevotionalAsync()
    {
        try
        {
            IsGenerating = true;
            GeneratingMessage = "✨ Creating your devotional with AI...";
            
            var today = DateTime.Today;
            TodaysDevotional = await _devotionalRepository.GenerateDevotionalAsync(today);
            _usageMetrics?.TrackDevotionalViewed();
            
            // Refresh the recent list
            await LoadRecentDevotionalsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Devotional] Error generating: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to generate devotional. Please check your AI connection.", "OK");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task LoadRecentDevotionalsAsync()
    {
        try
        {
            var recent = await _devotionalRepository.GetRecentDevotionalsAsync(7);
            RecentDevotionals = new ObservableCollection<Devotional>(recent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Devotional] Error loading history: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task MarkAsReadAsync()
    {
        if (TodaysDevotional == null) return;
        
        try
        {
            await _devotionalRepository.MarkDevotionalAsReadAsync(TodaysDevotional.Id);
            TodaysDevotional.IsRead = true;
            OnPropertyChanged(nameof(TodaysDevotional));
            
            await _dialogService.ShowAlertAsync("✓ Marked Complete", "Great job completing today's devotional!", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Devotional] Error marking as read: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ViewDevotionalAsync(Devotional? devotional)
    {
        if (devotional == null) return;
        
        TodaysDevotional = devotional;
        ShowHistory = false;
        _usageMetrics?.TrackDevotionalViewed();
    }

    [RelayCommand]
    private void ToggleHistory()
    {
        ShowHistory = !ShowHistory;
    }

    [RelayCommand]
    private async Task ShareDevotionalAsync()
    {
        if (TodaysDevotional == null) return;
        
        try
        {
            var shareText = $"📖 {TodaysDevotional.Title}\n\n" +
                           $"📜 {TodaysDevotional.ScriptureReference}\n" +
                           $"\"{TodaysDevotional.Scripture}\"\n\n" +
                           $"💭 {TodaysDevotional.Content}\n\n" +
                           $"🙏 {TodaysDevotional.Prayer}\n\n" +
                           $"— Voices of Scripture";
            
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = shareText,
                Title = "Share Devotional"
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Devotional] Error sharing: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyDevotionalAsync()
    {
        if (TodaysDevotional == null) return;

        var text = $"📖 {TodaysDevotional.Title}\n\n" +
                   $"📜 {TodaysDevotional.ScriptureReference}\n" +
                   $"\"{TodaysDevotional.Scripture}\"\n\n" +
                   $"💭 {TodaysDevotional.Content}\n\n" +
                   $"🙏 {TodaysDevotional.Prayer}\n\n" +
                   $"— Voices of Scripture";

        await Clipboard.Default.SetTextAsync(text);
        await _dialogService.ShowAlertAsync("Copied", "Devotional copied to clipboard.");
    }

    [RelayCommand]
    private async Task RefreshDevotionalAsync()
    {
        // Generate a fresh devotional for today (replaces existing)
        var confirm = await _dialogService.ShowConfirmAsync(
            "Generate New Devotional",
            "This will create a new AI-generated devotional for today, replacing the current one. Continue?",
            "Generate",
            "Cancel");
        
        if (confirm)
        {
            await GenerateDevotionalAsync();
        }
    }
}
