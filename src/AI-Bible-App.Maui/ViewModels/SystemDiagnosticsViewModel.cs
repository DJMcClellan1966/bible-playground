using AI_Bible_App.Maui.Services;
using AI_Bible_App.Maui.Services.Core;
using AI_Bible_App.Infrastructure.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045

namespace AI_Bible_App.Maui.ViewModels;

public partial class SystemDiagnosticsViewModel : BaseViewModel
{
    private readonly IPerformanceMonitor _performance;
    private readonly IIntelligentCacheService _cache;
    private readonly ICoreServicesOrchestrator _orchestrator;
    private readonly IChatEnhancementService _enhancement;
    private readonly IUsageMetricsService? _usageMetrics;
    private Timer? _refreshTimer;

    [ObservableProperty]
    private bool isHealthy;

    [ObservableProperty]
    private double cacheHitRate;

    [ObservableProperty]
    private long totalCacheEntries;

    [ObservableProperty]
    private long totalHits;

    [ObservableProperty]
    private long totalMisses;

    [ObservableProperty]
    private long similarityHits;

    [ObservableProperty]
    private double averageResponseTime;

    [ObservableProperty]
    private double peakResponseTime;

    [ObservableProperty]
    private ObservableCollection<MetricDisplay> recentMetrics = new();

    [ObservableProperty]
    private ObservableCollection<OperationDisplay> recentOperations = new();

    [ObservableProperty]
    private ObservableCollection<SuggestionDisplay> suggestions = new();

    [ObservableProperty]
    private string systemStatus = "Loading...";

    [ObservableProperty]
    private string lastRefreshed = "";

    [ObservableProperty]
    private bool isRefreshing;

    public SystemDiagnosticsViewModel(
        IPerformanceMonitor performance,
        IIntelligentCacheService cache,
        ICoreServicesOrchestrator orchestrator,
        IChatEnhancementService enhancement,
        IUsageMetricsService? usageMetrics = null)
    {
        _performance = performance;
        _cache = cache;
        _orchestrator = orchestrator;
        _enhancement = enhancement;
        _usageMetrics = usageMetrics;
        Title = "System Diagnostics";
    }

    public async Task InitializeAsync()
    {
        _usageMetrics?.TrackFeatureUsed("SystemDiagnostics");
        await RefreshDataAsync();
        
        // Auto-refresh every 5 seconds
        _refreshTimer = new Timer(async _ =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await RefreshDataAsync());
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (IsRefreshing) return;
        
        try
        {
            IsRefreshing = true;
            
            // Get cache statistics
            var cacheStats = _cache.GetStatistics();
            CacheHitRate = cacheStats.HitRate;
            TotalCacheEntries = cacheStats.TotalItems;
            TotalHits = cacheStats.TotalHits;
            TotalMisses = cacheStats.TotalMisses;
            SimilarityHits = cacheStats.SimilarityHits;

            // Get performance report
            var report = _performance.GetReport(TimeSpan.FromMinutes(30));
            
            // Update operations display
            RecentOperations.Clear();
            foreach (var op in report.OperationStats.Values.Take(10))
            {
                RecentOperations.Add(new OperationDisplay
                {
                    Name = op.Name,
                    Category = op.Category ?? "General",
                    AvgDuration = $"{op.AverageDuration.TotalMilliseconds:F1}ms",
                    CallCount = op.TotalCount,
                    SuccessRate = $"{op.SuccessRate:F0}%"
                });
            }

            // Update metrics display
            RecentMetrics.Clear();
            foreach (var metric in report.MetricStats.Values.Take(10))
            {
                RecentMetrics.Add(new MetricDisplay
                {
                    Name = metric.Name,
                    Value = $"{metric.CurrentValue:F2}{metric.Unit}",
                    Time = DateTime.Now.ToString("HH:mm:ss")
                });
            }

            // Get health check
            var health = await _orchestrator.GetSystemHealthAsync();
            IsHealthy = health.IsHealthy;
            SystemStatus = health.IsHealthy ? "✅ All Systems Operational" : "⚠️ Performance Issues Detected";

            // Update suggestions
            Suggestions.Clear();
            foreach (var suggestion in health.Suggestions.Take(5))
            {
                Suggestions.Add(new SuggestionDisplay
                {
                    Area = suggestion.Area,
                    Issue = suggestion.Issue,
                    Suggestion = suggestion.Suggestion,
                    Impact = suggestion.Impact,
                    PriorityColor = suggestion.Priority switch
                    {
                        Priority.High => Colors.Red,
                        Priority.Medium => Colors.Orange,
                        Priority.Low => Colors.Green,
                        _ => Colors.Gray
                    }
                });
            }

            // Calculate average response time
            var responseTimes = report.MetricStats.Values
                .Where(m => m.Name.Contains("response") || m.Name.Contains("duration"))
                .Select(m => m.CurrentValue)
                .ToList();
            
            if (responseTimes.Any())
            {
                AverageResponseTime = responseTimes.Average();
                PeakResponseTime = responseTimes.Max();
            }

            LastRefreshed = DateTime.Now.ToString("HH:mm:ss");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task WarmupSystemAsync()
    {
        try
        {
            SystemStatus = "🔄 Warming up systems...";
            await _orchestrator.WarmupAsync();
            await RefreshDataAsync();
            SystemStatus = "✅ Warmup Complete";
        }
        catch (Exception ex)
        {
            SystemStatus = $"⚠️ Warmup failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            _cache.ClearExpired();
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Diagnostics] Clear cache error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TestEnhancementAsync()
    {
        try
        {
            SystemStatus = "🔄 Running enhancement test...";
            
            var testCharacter = new AI_Bible_App.Core.Models.BiblicalCharacter
            {
                Id = "peter",
                Name = "Peter"
            };
            
            var enhancement = await _enhancement.EnhanceBeforeResponseAsync(
                testCharacter,
                "What is faith?",
                Enumerable.Empty<AI_Bible_App.Core.Models.ChatMessage>());
            
            SystemStatus = $"✅ Test Complete - Mood: {enhancement.CharacterMood.PrimaryMood}, " +
                          $"Verses: {enhancement.RelevantVerses.Count}, " +
                          $"Time: {enhancement.ProcessingTime.TotalMilliseconds:F0}ms";
            
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            SystemStatus = $"⚠️ Test failed: {ex.Message}";
        }
    }

    public void Cleanup()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }
}

public class MetricDisplay
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Time { get; set; } = "";
}

public class OperationDisplay
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string AvgDuration { get; set; } = "";
    public int CallCount { get; set; }
    public string SuccessRate { get; set; } = "";
}

public class SuggestionDisplay
{
    public string Area { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public string Impact { get; set; } = "";
    public Color PriorityColor { get; set; } = Colors.Gray;
}
