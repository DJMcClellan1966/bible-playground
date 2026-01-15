using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Infrastructure.Services;
using CommunityToolkit.Mvvm.ComponentModel;

#pragma warning disable MVVMTK0045 // AOT compatibility warning for WinRT scenarios

namespace AI_Bible_App.Maui.ViewModels;

public partial class InitializationViewModel : BaseViewModel
{
    private readonly IBibleRAGService _ragService;
    private readonly IHealthCheckService _healthCheckService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private string statusMessage = "Initializing...";

    [ObservableProperty]
    private double progress = 0;

    [ObservableProperty]
    private bool isInitializing = true;

    [ObservableProperty]
    private bool hasError = false;

    [ObservableProperty]
    private string? errorMessage;

    public InitializationViewModel(
        IBibleRAGService ragService,
        IHealthCheckService healthCheckService,
        IUsageMetricsService? usageMetrics = null)
    {
        _ragService = ragService;
        _healthCheckService = healthCheckService;
        _usageMetrics = usageMetrics;
        Title = "Voices of Scripture";
    }

    public async Task InitializeAsync()
    {
        try
        {
            _usageMetrics?.TrackFeatureUsed("Initialization");
            // Quick startup - just verify AI service is reachable
            StatusMessage = "Connecting...";
            Progress = 0.3;

            // Run health check with short timeout for fast startup
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                var healthTask = _healthCheckService.GetHealthStatusAsync();
                var completedTask = await Task.WhenAny(healthTask, Task.Delay(3000, cts.Token));
                
                if (completedTask == healthTask)
                {
                    var health = await healthTask;
                    if (!health.IsHealthy)
                    {
                        // Don't block - user can still use app with cloud fallback
                        System.Diagnostics.Debug.WriteLine($"[WARN] Local AI not available: {health.ErrorMessage}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Health check timed out - continue anyway
                System.Diagnostics.Debug.WriteLine("[WARN] Health check timed out, continuing...");
            }

            Progress = 0.7;
            
            // LAZY RAG: Don't initialize here - let it load on first chat
            // This saves 2-5 seconds on startup
            // RAG will be initialized in LocalAIService.GetChatResponseAsync when needed
            
            Progress = 1.0;
            StatusMessage = "Ready!";
            IsInitializing = false;
        }
        catch (Exception ex)
        {
            // Don't block on errors - app can still work with cloud AI
            System.Diagnostics.Debug.WriteLine($"[ERROR] Initialization: {ex.Message}");
            HasError = false; // Changed: don't show error, just continue
            IsInitializing = false;
        }
    }
}
