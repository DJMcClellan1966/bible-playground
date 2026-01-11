using System.Collections.Concurrent;
using System.Diagnostics;

namespace AI_Bible_App.Maui.Services.Core;

/// <summary>
/// Real-time performance monitoring service that tracks app metrics,
/// identifies bottlenecks, and provides optimization suggestions.
/// </summary>
public interface IPerformanceMonitor
{
    IDisposable BeginOperation(string operationName, string? category = null);
    void RecordMetric(string name, double value, string? unit = null);
    void RecordEvent(string eventName, Dictionary<string, string>? properties = null);
    PerformanceReport GetReport(TimeSpan? window = null);
    Task<HealthCheck> CheckHealthAsync();
    void SetThreshold(string metricName, double warningThreshold, double criticalThreshold);
    event EventHandler<PerformanceAlert>? OnAlert;
}

public class PerformanceReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ReportWindow { get; set; }
    public Dictionary<string, OperationStats> OperationStats { get; set; } = new();
    public Dictionary<string, MetricStats> MetricStats { get; set; } = new();
    public List<PerformanceEvent> RecentEvents { get; set; } = new();
    public List<PerformanceAlert> ActiveAlerts { get; set; } = new();
    public SystemMetrics SystemMetrics { get; set; } = new();
    public List<OptimizationSuggestion> Suggestions { get; set; } = new();
}

public class OperationStats
{
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public int TotalCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public TimeSpan P95Duration { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount * 100 : 0;
}

public class MetricStats
{
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public double CurrentValue { get; set; }
    public double AverageValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double StandardDeviation { get; set; }
    public List<(DateTime Time, double Value)> History { get; set; } = new();
}

public class PerformanceEvent
{
    public DateTime Timestamp { get; set; }
    public string Name { get; set; } = "";
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class PerformanceAlert
{
    public DateTime Timestamp { get; set; }
    public AlertSeverity Severity { get; set; }
    public string MetricName { get; set; } = "";
    public string Message { get; set; } = "";
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
}

public enum AlertSeverity { Info, Warning, Critical }

public class SystemMetrics
{
    public long MemoryUsageBytes { get; set; }
    public double MemoryUsageMB => MemoryUsageBytes / (1024.0 * 1024.0);
    public int ThreadCount { get; set; }
    public TimeSpan Uptime { get; set; }
    public int ActiveOperations { get; set; }
}

public class HealthCheck
{
    public bool IsHealthy { get; set; }
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

public class ComponentHealth
{
    public string Name { get; set; } = "";
    public bool IsHealthy { get; set; }
    public string? Status { get; set; }
    public TimeSpan? ResponseTime { get; set; }
}

public class OptimizationSuggestion
{
    public string Area { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public string Impact { get; set; } = "";
    public Priority Priority { get; set; }
}

public enum Priority { Low, Medium, High }

public class PerformanceMonitor : IPerformanceMonitor, IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<OperationRecord>> _operations = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<MetricRecord>> _metrics = new();
    private readonly ConcurrentBag<PerformanceEvent> _events = new();
    private readonly ConcurrentDictionary<string, (double Warning, double Critical)> _thresholds = new();
    private readonly ConcurrentBag<PerformanceAlert> _alerts = new();
    private readonly Timer _cleanupTimer;
    private readonly Timer _analysisTimer;
    private readonly DateTime _startTime;
    private int _activeOperations;
    
    private const int MaxRecordsPerMetric = 1000;
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(2);

    public event EventHandler<PerformanceAlert>? OnAlert;

    public PerformanceMonitor()
    {
        _startTime = DateTime.UtcNow;
        
        // Cleanup old records every 10 minutes
        _cleanupTimer = new Timer(_ => CleanupOldRecords(), null, 
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        
        // Analyze performance every 30 seconds
        _analysisTimer = new Timer(_ => AnalyzePerformance(), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        // Set default thresholds
        SetDefaultThresholds();
    }

    public IDisposable BeginOperation(string operationName, string? category = null)
    {
        Interlocked.Increment(ref _activeOperations);
        return new OperationScope(this, operationName, category);
    }

    public void RecordMetric(string name, double value, string? unit = null)
    {
        var bag = _metrics.GetOrAdd(name, _ => new ConcurrentBag<MetricRecord>());
        bag.Add(new MetricRecord
        {
            Timestamp = DateTime.UtcNow,
            Value = value,
            Unit = unit
        });
        
        // Check thresholds
        if (_thresholds.TryGetValue(name, out var threshold))
        {
            if (value >= threshold.Critical)
            {
                RaiseAlert(name, value, threshold.Critical, AlertSeverity.Critical);
            }
            else if (value >= threshold.Warning)
            {
                RaiseAlert(name, value, threshold.Warning, AlertSeverity.Warning);
            }
        }
    }

    public void RecordEvent(string eventName, Dictionary<string, string>? properties = null)
    {
        _events.Add(new PerformanceEvent
        {
            Timestamp = DateTime.UtcNow,
            Name = eventName,
            Properties = properties ?? new Dictionary<string, string>()
        });
    }

    public PerformanceReport GetReport(TimeSpan? window = null)
    {
        var reportWindow = window ?? TimeSpan.FromHours(1);
        var cutoff = DateTime.UtcNow - reportWindow;
        
        var report = new PerformanceReport
        {
            ReportWindow = reportWindow,
            SystemMetrics = GetSystemMetrics(),
            ActiveAlerts = _alerts.Where(a => a.Timestamp > cutoff).ToList()
        };
        
        // Calculate operation stats
        foreach (var (name, records) in _operations)
        {
            var recentRecords = records.Where(r => r.EndTime > cutoff).ToList();
            if (recentRecords.Count == 0) continue;
            
            var durations = recentRecords
                .Where(r => r.Duration.HasValue)
                .Select(r => r.Duration!.Value)
                .OrderBy(d => d)
                .ToList();
            
            if (durations.Count == 0) continue;
            
            report.OperationStats[name] = new OperationStats
            {
                Name = name,
                Category = recentRecords.FirstOrDefault()?.Category,
                TotalCount = recentRecords.Count,
                TotalDuration = TimeSpan.FromTicks(durations.Sum(d => d.Ticks)),
                AverageDuration = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks)),
                MinDuration = durations.First(),
                MaxDuration = durations.Last(),
                P95Duration = durations[(int)(durations.Count * 0.95)],
                SuccessCount = recentRecords.Count(r => r.Success),
                FailureCount = recentRecords.Count(r => !r.Success)
            };
        }
        
        // Calculate metric stats
        foreach (var (name, records) in _metrics)
        {
            var recentRecords = records.Where(r => r.Timestamp > cutoff).ToList();
            if (recentRecords.Count == 0) continue;
            
            var values = recentRecords.Select(r => r.Value).ToList();
            var avg = values.Average();
            var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / values.Count);
            
            report.MetricStats[name] = new MetricStats
            {
                Name = name,
                Unit = recentRecords.FirstOrDefault()?.Unit,
                CurrentValue = values.Last(),
                AverageValue = avg,
                MinValue = values.Min(),
                MaxValue = values.Max(),
                StandardDeviation = stdDev,
                History = recentRecords
                    .OrderByDescending(r => r.Timestamp)
                    .Take(50)
                    .Select(r => (r.Timestamp, r.Value))
                    .ToList()
            };
        }
        
        // Get recent events
        report.RecentEvents = _events
            .Where(e => e.Timestamp > cutoff)
            .OrderByDescending(e => e.Timestamp)
            .Take(100)
            .ToList();
        
        // Generate suggestions
        report.Suggestions = GenerateSuggestions(report);
        
        return report;
    }

    public async Task<HealthCheck> CheckHealthAsync()
    {
        var health = new HealthCheck { IsHealthy = true };
        
        // Check memory
        var memoryUsage = GC.GetTotalMemory(false);
        var memoryMB = memoryUsage / (1024.0 * 1024.0);
        health.Components["Memory"] = new ComponentHealth
        {
            Name = "Memory",
            IsHealthy = memoryMB < 500,
            Status = $"{memoryMB:F1} MB used"
        };
        if (memoryMB >= 500)
        {
            health.IsHealthy = false;
            health.Issues.Add("High memory usage detected");
        }
        
        // Check active operations
        health.Components["Operations"] = new ComponentHealth
        {
            Name = "Active Operations",
            IsHealthy = _activeOperations < 50,
            Status = $"{_activeOperations} active"
        };
        if (_activeOperations >= 50)
        {
            health.IsHealthy = false;
            health.Issues.Add("Too many concurrent operations");
        }
        
        // Check response times
        var avgResponseTime = GetAverageResponseTime();
        health.Components["ResponseTime"] = new ComponentHealth
        {
            Name = "Response Time",
            IsHealthy = avgResponseTime < TimeSpan.FromSeconds(5),
            Status = $"{avgResponseTime.TotalMilliseconds:F0}ms average",
            ResponseTime = avgResponseTime
        };
        if (avgResponseTime >= TimeSpan.FromSeconds(5))
        {
            health.IsHealthy = false;
            health.Issues.Add("Slow response times detected");
        }
        
        // Check cache health
        var cacheHitRate = GetCacheHitRate();
        health.Components["Cache"] = new ComponentHealth
        {
            Name = "Cache",
            IsHealthy = cacheHitRate > 0.5 || cacheHitRate == 0,
            Status = cacheHitRate > 0 ? $"{cacheHitRate * 100:F1}% hit rate" : "No cache data"
        };
        
        return await Task.FromResult(health);
    }

    public void SetThreshold(string metricName, double warningThreshold, double criticalThreshold)
    {
        _thresholds[metricName] = (warningThreshold, criticalThreshold);
    }

    private void SetDefaultThresholds()
    {
        SetThreshold("memory_mb", 400, 600);
        SetThreshold("response_time_ms", 3000, 10000);
        SetThreshold("error_rate", 0.05, 0.15);
        SetThreshold("active_operations", 30, 50);
    }

    private void RaiseAlert(string metricName, double value, double threshold, AlertSeverity severity)
    {
        var alert = new PerformanceAlert
        {
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            MetricName = metricName,
            Message = $"{metricName} ({value:F2}) exceeded {severity.ToString().ToLower()} threshold ({threshold:F2})",
            CurrentValue = value,
            ThresholdValue = threshold
        };
        
        _alerts.Add(alert);
        OnAlert?.Invoke(this, alert);
    }

    private SystemMetrics GetSystemMetrics()
    {
        return new SystemMetrics
        {
            MemoryUsageBytes = GC.GetTotalMemory(false),
            ThreadCount = Process.GetCurrentProcess().Threads.Count,
            Uptime = DateTime.UtcNow - _startTime,
            ActiveOperations = _activeOperations
        };
    }

    private TimeSpan GetAverageResponseTime()
    {
        var recentOps = _operations.Values
            .SelectMany(bag => bag)
            .Where(r => r.EndTime > DateTime.UtcNow - TimeSpan.FromMinutes(5) && r.Duration.HasValue)
            .Select(r => r.Duration!.Value)
            .ToList();
        
        if (recentOps.Count == 0) return TimeSpan.Zero;
        return TimeSpan.FromTicks((long)recentOps.Average(d => d.Ticks));
    }

    private double GetCacheHitRate()
    {
        if (!_metrics.TryGetValue("cache_hit_rate", out var records))
            return 0;
        
        var recent = records
            .Where(r => r.Timestamp > DateTime.UtcNow - TimeSpan.FromMinutes(5))
            .ToList();
        
        return recent.Count > 0 ? recent.Average(r => r.Value) : 0;
    }

    private List<OptimizationSuggestion> GenerateSuggestions(PerformanceReport report)
    {
        var suggestions = new List<OptimizationSuggestion>();
        
        // Check for slow operations
        foreach (var (name, stats) in report.OperationStats)
        {
            if (stats.AverageDuration > TimeSpan.FromSeconds(3))
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Area = "Performance",
                    Issue = $"Operation '{name}' averages {stats.AverageDuration.TotalSeconds:F1}s",
                    Suggestion = "Consider caching results or optimizing the operation",
                    Impact = "Improved response times",
                    Priority = stats.AverageDuration > TimeSpan.FromSeconds(5) ? Priority.High : Priority.Medium
                });
            }
            
            if (stats.SuccessRate < 95)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Area = "Reliability",
                    Issue = $"Operation '{name}' has {stats.SuccessRate:F1}% success rate",
                    Suggestion = "Investigate failure causes and add retry logic",
                    Impact = "Improved reliability",
                    Priority = stats.SuccessRate < 80 ? Priority.High : Priority.Medium
                });
            }
        }
        
        // Check memory usage
        if (report.SystemMetrics.MemoryUsageMB > 300)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Area = "Memory",
                Issue = $"Memory usage at {report.SystemMetrics.MemoryUsageMB:F1} MB",
                Suggestion = "Consider clearing caches or reducing retained data",
                Impact = "Reduced memory footprint",
                Priority = report.SystemMetrics.MemoryUsageMB > 500 ? Priority.High : Priority.Low
            });
        }
        
        // Check for high metric variance
        foreach (var (name, stats) in report.MetricStats)
        {
            if (stats.StandardDeviation > stats.AverageValue * 0.5 && stats.AverageValue > 0)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Area = "Stability",
                    Issue = $"Metric '{name}' shows high variance (σ={stats.StandardDeviation:F2})",
                    Suggestion = "Investigate causes of inconsistent behavior",
                    Impact = "More predictable performance",
                    Priority = Priority.Medium
                });
            }
        }
        
        return suggestions.OrderByDescending(s => s.Priority).ToList();
    }

    private void CleanupOldRecords()
    {
        var cutoff = DateTime.UtcNow - RetentionPeriod;
        
        // Cleanup operations
        foreach (var (name, bag) in _operations)
        {
            var recent = bag.Where(r => r.EndTime > cutoff).ToList();
            if (recent.Count < bag.Count)
            {
                _operations[name] = new ConcurrentBag<OperationRecord>(recent);
            }
        }
        
        // Cleanup metrics
        foreach (var (name, bag) in _metrics)
        {
            var recent = bag
                .Where(r => r.Timestamp > cutoff)
                .OrderByDescending(r => r.Timestamp)
                .Take(MaxRecordsPerMetric)
                .ToList();
            
            if (recent.Count < bag.Count)
            {
                _metrics[name] = new ConcurrentBag<MetricRecord>(recent);
            }
        }
        
        // Cleanup alerts
        var recentAlerts = _alerts.Where(a => a.Timestamp > cutoff).ToList();
        while (_alerts.TryTake(out _)) { }
        foreach (var alert in recentAlerts)
        {
            _alerts.Add(alert);
        }
    }

    private void AnalyzePerformance()
    {
        // Record system metrics
        RecordMetric("memory_mb", GC.GetTotalMemory(false) / (1024.0 * 1024.0), "MB");
        RecordMetric("active_operations", _activeOperations, "count");
        RecordMetric("thread_count", Process.GetCurrentProcess().Threads.Count, "count");
        
        // Calculate and record error rate
        var recentOps = _operations.Values
            .SelectMany(bag => bag)
            .Where(r => r.EndTime > DateTime.UtcNow - TimeSpan.FromMinutes(5))
            .ToList();
        
        if (recentOps.Count > 0)
        {
            var errorRate = (double)recentOps.Count(r => !r.Success) / recentOps.Count;
            RecordMetric("error_rate", errorRate, "ratio");
        }
    }

    internal void CompleteOperation(string name, string? category, TimeSpan duration, bool success)
    {
        Interlocked.Decrement(ref _activeOperations);
        
        var bag = _operations.GetOrAdd(name, _ => new ConcurrentBag<OperationRecord>());
        bag.Add(new OperationRecord
        {
            Name = name,
            Category = category,
            StartTime = DateTime.UtcNow - duration,
            EndTime = DateTime.UtcNow,
            Duration = duration,
            Success = success
        });
        
        // Record response time metric
        RecordMetric($"response_time_{name}", duration.TotalMilliseconds, "ms");
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _analysisTimer?.Dispose();
    }

    private class OperationRecord
    {
        public string Name { get; set; } = "";
        public string? Category { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool Success { get; set; }
    }

    private class MetricRecord
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Unit { get; set; }
    }

    private class OperationScope : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _name;
        private readonly string? _category;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;
        private bool _success = true;

        public OperationScope(PerformanceMonitor monitor, string name, string? category)
        {
            _monitor = monitor;
            _name = name;
            _category = category;
            _stopwatch = Stopwatch.StartNew();
        }

        public void MarkFailed() => _success = false;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            _stopwatch.Stop();
            _monitor.CompleteOperation(_name, _category, _stopwatch.Elapsed, _success);
        }
    }
}
