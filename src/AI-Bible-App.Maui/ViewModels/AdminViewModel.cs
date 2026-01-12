namespace AI_Bible_App.Maui.ViewModels;

/// <summary>
/// Admin page for managing autonomous learning cycles
/// </summary>
public partial class AdminViewModel : ObservableObject
{
    private readonly IAutonomousLearningService _learningService;
    private readonly ITrainingDataRepository _trainingRepo;
    private readonly ILogger<AdminViewModel> _logger;
    
    [ObservableProperty]
    private LearningStatistics? statistics;
    
    [ObservableProperty]
    private bool isExecutingCycle;
    
    [ObservableProperty]
    private string? lastCycleMessage;
    
    [ObservableProperty]
    private double cycleProgress;
    
    [ObservableProperty]
    private int availableConversations;
    
    [ObservableProperty]
    private bool canStartCycle;
    
    public AdminViewModel(
        IAutonomousLearningService learningService,
        ITrainingDataRepository trainingRepo,
        ILogger<AdminViewModel> logger)
    {
        _learningService = learningService;
        _trainingRepo = trainingRepo;
        _logger = logger;
    }
    
    public async Task InitializeAsync()
    {
        await RefreshStatisticsAsync();
    }
    
    [RelayCommand]
    private async Task RefreshStatisticsAsync()
    {
        try
        {
            Statistics = await _learningService.GetLearningStatisticsAsync();
            
            // Check available conversations
            var conversations = await _trainingRepo.GetHighQualityConversationsAsync(4.0, 100);
            AvailableConversations = conversations.Count;
            
            // Check if cycle can be started
            CanStartCycle = await _learningService.ShouldTriggerLearningCycleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing statistics");
        }
    }
    
    [RelayCommand]
    private async Task StartLearningCycleAsync()
    {
        if (IsExecutingCycle)
            return;
            
        try
        {
            IsExecutingCycle = true;
            CycleProgress = 0;
            LastCycleMessage = "Collecting training data...";
            
            var result = await _learningService.ExecuteLearningCycleAsync();
            
            CycleProgress = 1.0;
            
            if (result.Success)
            {
                if (result.ModelDeployed)
                {
                    LastCycleMessage = $"✅ Success! {result.DeploymentMessage}\n" +
                                     $"New version: {result.NewModelVersion}\n" +
                                     $"Improvement: {result.ImprovementScore:+P1}\n" +
                                     $"Duration: {(result.CompletedAt - result.StartedAt).TotalMinutes:F1} minutes";
                }
                else
                {
                    LastCycleMessage = $"⚠️ Cycle completed but model not deployed.\n" +
                                     $"{result.DeploymentMessage}\n" +
                                     $"Duration: {(result.CompletedAt - result.StartedAt).TotalMinutes:F1} minutes";
                }
            }
            else
            {
                LastCycleMessage = $"❌ Learning cycle failed:\n{string.Join("\n", result.Errors)}";
            }
            
            await RefreshStatisticsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during learning cycle");
            LastCycleMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsExecutingCycle = false;
        }
    }
    
    [RelayCommand]
    private async Task GenerateTrainingDataAsync(string mode)
    {
        // Mode: "synthetic" or "multi-character"
        try
        {
            LastCycleMessage = $"Generating {mode} training data...";
            
            // This would trigger generation of synthetic conversations
            // Implementation depends on whether we want to generate on-demand
            
            await RefreshStatisticsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating training data");
            LastCycleMessage = $"❌ Error: {ex.Message}";
        }
    }
}
