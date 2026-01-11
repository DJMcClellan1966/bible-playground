using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Accessibility;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045

namespace AI_Bible_App.Maui.ViewModels;

public partial class ReadingPlanViewModel : BaseViewModel
{
    private readonly IReadingPlanRepository _repository;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private ReadingPlan? activePlan;

    [ObservableProperty]
    private UserReadingProgress? activeProgress;

    [ObservableProperty]
    private ReadingPlanDay? todaysReading;

    [ObservableProperty]
    private ObservableCollection<ReadingPlanItemViewModel> availablePlans = new();

    [ObservableProperty]
    private ObservableCollection<CompletedPlanViewModel> completedProgress = new();

    public ReadingPlanViewModel(IReadingPlanRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        Title = "Reading Plans";
    }

    public bool HasActivePlan => ActivePlan != null && ActiveProgress != null;
    public bool ShowNoPlanMessage => !IsLoading && !HasActivePlan;
    public bool HasCompletedPlans => CompletedProgress.Count > 0;
    
    public double ProgressPercent => ActiveProgress != null 
        ? ActiveProgress.CompletionPercentage / 100.0 
        : 0;
    
    public string ProgressText => ActiveProgress != null 
        ? $"{ActiveProgress.CompletedDays.Count}/{ActiveProgress.TotalDays} days ({ActiveProgress.CompletionPercentage:F0}%)" 
        : "";
    
    public string StreakText => ActiveProgress != null 
        ? $"🔥 {ActiveProgress.CurrentStreak} day streak (Best: {ActiveProgress.LongestStreak})" 
        : "";

    public string TodaysPassagesText => TodaysReading != null 
        ? string.Join(", ", TodaysReading.Passages) 
        : "";

    public bool HasKeyVerse => !string.IsNullOrEmpty(TodaysReading?.KeyVerse);
    public bool HasReflectionPrompt => !string.IsNullOrEmpty(TodaysReading?.ReflectionPrompt);
    
    public bool IsTodayCompleted => ActiveProgress != null && TodaysReading != null 
        && ActiveProgress.CompletedDays.Contains(TodaysReading.DayNumber);
    
    public bool CanMarkComplete => ActiveProgress != null && TodaysReading != null 
        && !ActiveProgress.CompletedDays.Contains(TodaysReading.DayNumber);

    public bool CanGoPrevious => ActiveProgress != null && ActiveProgress.CurrentDay > 1;
    public bool CanGoNext => ActiveProgress != null && ActiveProgress.CurrentDay < ActiveProgress.TotalDays;

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            // Load all available plans
            var plans = await _repository.GetAllPlansAsync();
            AvailablePlans.Clear();
            foreach (var plan in plans)
            {
                AvailablePlans.Add(new ReadingPlanItemViewModel(plan, StartPlanAsync));
            }

            // Load active progress
            ActiveProgress = await _repository.GetActiveProgressAsync();
            if (ActiveProgress != null)
            {
                ActivePlan = await _repository.GetPlanByIdAsync(ActiveProgress.PlanId);
                TodaysReading = ActivePlan?.Days.FirstOrDefault(d => d.DayNumber == ActiveProgress.CurrentDay);
            }
            else
            {
                ActivePlan = null;
                TodaysReading = null;
            }

            // Load completed plans
            var allProgress = await _repository.GetAllProgressAsync();
            CompletedProgress.Clear();
            foreach (var progress in allProgress.Where(p => p.CompletedAt != null))
            {
                var plan = await _repository.GetPlanByIdAsync(progress.PlanId);
                if (plan != null)
                {
                    CompletedProgress.Add(new CompletedPlanViewModel
                    {
                        PlanName = plan.Name,
                        CompletedDate = progress.CompletedAt!.Value
                    });
                }
            }

            NotifyPropertiesChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReadingPlan] Error loading: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to load reading plans.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadDataAsync();

    private async Task StartPlanAsync(string planId)
    {
        try
        {
            if (ActiveProgress != null)
            {
                var confirm = await _dialogService.ShowConfirmAsync(
                    "Start New Plan?",
                    "You have an active plan. Starting a new plan will abandon your current progress. Continue?",
                    "Yes, Start New", "Cancel");
                
                if (!confirm)
                    return;
            }

            IsLoading = true;
            ActiveProgress = await _repository.StartPlanAsync(planId);
            await LoadDataAsync();
            
            // Announce to screen readers
            var plan = await _repository.GetPlanByIdAsync(planId);
            SemanticScreenReader.Announce($"Started reading plan: {plan?.Name ?? "New plan"}");
            
            await _dialogService.ShowAlertAsync("Plan Started!", "Your reading plan has begun. Happy reading! 📖", "Let's Go!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReadingPlan] Error starting plan: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to start the reading plan.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MarkCompleted()
    {
        if (ActiveProgress == null || TodaysReading == null)
            return;

        try
        {
            ActiveProgress = await _repository.MarkDayCompletedAsync(ActiveProgress.Id, TodaysReading.DayNumber);
            
            // Announce to screen readers
            SemanticScreenReader.Announce($"Day {TodaysReading.DayNumber} marked complete. {ActiveProgress.CompletionPercentage:F0}% done.");
            
            // Check if plan is complete
            if (ActiveProgress.CompletedAt != null)
            {
                SemanticScreenReader.Announce($"Congratulations! You've completed the {ActivePlan?.Name} reading plan!");
                await _dialogService.ShowAlertAsync(
                    "🎉 Congratulations!",
                    $"You've completed the {ActivePlan?.Name} reading plan! What an accomplishment!",
                    "Celebrate!");
            }
            
            await LoadDataAsync();;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReadingPlan] Error marking complete: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to mark day as completed.", "OK");
        }
    }

    [RelayCommand]
    private async Task OpenPassage()
    {
        if (TodaysReading == null || !TodaysReading.Passages.Any())
            return;

        // Navigate to Bible reader with the first passage
        var passage = TodaysReading.Passages.First();
        // This would navigate to BibleReaderPage with the passage reference
        // For now, show the passage
        await _dialogService.ShowAlertAsync(
            "Today's Reading",
            $"Open your Bible to: {string.Join(", ", TodaysReading.Passages)}",
            "OK");
    }

    [RelayCommand]
    private async Task PreviousDay()
    {
        if (ActiveProgress == null || ActiveProgress.CurrentDay <= 1)
            return;

        try
        {
            await _repository.UpdateCurrentDayAsync(ActiveProgress.Id, ActiveProgress.CurrentDay - 1);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReadingPlan] Error navigating: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task NextDay()
    {
        if (ActiveProgress == null || ActiveProgress.CurrentDay >= ActiveProgress.TotalDays)
            return;

        try
        {
            await _repository.UpdateCurrentDayAsync(ActiveProgress.Id, ActiveProgress.CurrentDay + 1);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReadingPlan] Error navigating: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AbandonPlan()
    {
        if (ActiveProgress == null)
            return;

        var confirm = await _dialogService.ShowConfirmAsync(
            "Abandon Plan?",
            "Are you sure you want to abandon your current reading plan? Your progress will be lost.",
            "Yes, Abandon", "Cancel");

        if (!confirm)
            return;

        try
        {
            await _repository.DeleteProgressAsync(ActiveProgress.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReadingPlan] Error abandoning: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to abandon the plan.", "OK");
        }
    }

    private void NotifyPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasActivePlan));
        OnPropertyChanged(nameof(ShowNoPlanMessage));
        OnPropertyChanged(nameof(HasCompletedPlans));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(StreakText));
        OnPropertyChanged(nameof(TodaysPassagesText));
        OnPropertyChanged(nameof(HasKeyVerse));
        OnPropertyChanged(nameof(HasReflectionPrompt));
        OnPropertyChanged(nameof(IsTodayCompleted));
        OnPropertyChanged(nameof(CanMarkComplete));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }
}

/// <summary>
/// View model for displaying a reading plan in the list
/// </summary>
public partial class ReadingPlanItemViewModel : ObservableObject
{
    private readonly Func<string, Task> _startAction;

    public ReadingPlanItemViewModel(ReadingPlan plan, Func<string, Task> startAction)
    {
        Plan = plan;
        _startAction = startAction;
        StartCommand = new AsyncRelayCommand(() => _startAction(Plan.Id));
    }

    public ReadingPlan Plan { get; }
    
    public string Name => Plan.Name;
    public string Description => Plan.Description;
    public int TotalDays => Plan.TotalDays;
    public int EstimatedMinutesPerDay => Plan.EstimatedMinutesPerDay;
    
    public string DifficultyBadge => Plan.Difficulty switch
    {
        ReadingPlanDifficulty.Light => "Light",
        ReadingPlanDifficulty.Medium => "Medium",
        ReadingPlanDifficulty.Intensive => "Intensive",
        _ => ""
    };
    
    public Color DifficultyColor => Plan.Difficulty switch
    {
        ReadingPlanDifficulty.Light => Colors.Green,
        ReadingPlanDifficulty.Medium => Colors.Orange,
        ReadingPlanDifficulty.Intensive => Colors.Red,
        _ => Colors.Gray
    };
    
    public string TypeBadge => Plan.Type switch
    {
        ReadingPlanType.Canonical => "📖 Canonical",
        ReadingPlanType.Chronological => "📅 Chronological",
        ReadingPlanType.Thematic => "🎯 Thematic",
        ReadingPlanType.Gospel => "✝️ Gospel",
        ReadingPlanType.NewTestament => "📜 New Testament",
        ReadingPlanType.OldTestament => "📜 Old Testament",
        ReadingPlanType.Wisdom => "💡 Wisdom",
        ReadingPlanType.Prophets => "📢 Prophets",
        _ => ""
    };
    
    public IAsyncRelayCommand StartCommand { get; }
}

/// <summary>
/// View model for displaying a completed plan
/// </summary>
public class CompletedPlanViewModel
{
    public string PlanName { get; set; } = "";
    public DateTime CompletedDate { get; set; }
}
