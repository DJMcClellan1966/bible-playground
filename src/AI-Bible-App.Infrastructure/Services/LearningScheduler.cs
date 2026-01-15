using AI_Bible_App.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Runs autonomous learning cycles during off-hours when enough data exists.
/// </summary>
public class LearningScheduler
{
    private readonly ILogger<LearningScheduler> _logger;
    private readonly IAutonomousLearningService _learningService;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
    private readonly CancellationTokenSource _cancellationSource = new();
    private Task? _schedulerTask;
    private DateTime _lastRun = DateTime.MinValue;

    public LearningScheduler(
        ILogger<LearningScheduler> logger,
        IAutonomousLearningService learningService,
        IConfiguration configuration)
    {
        _logger = logger;
        _learningService = learningService;
        _configuration = configuration;
    }

    public void Start()
    {
        _logger.LogInformation("Learning Scheduler starting");
        _schedulerTask = ExecuteAsync(_cancellationSource.Token);
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Learning Scheduler stopping");
        _cancellationSource.Cancel();
        if (_schedulerTask != null)
            await _schedulerTask;
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Learning Scheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);

                var enabled = _configuration["AutonomousLearning:Enabled"] == "true";
                if (!enabled)
                {
                    _logger.LogDebug("Autonomous learning disabled");
                    continue;
                }

                var now = DateTime.Now;
                var startHour = int.TryParse(_configuration["AutonomousLearning:StartHour"], out var sh) ? sh : 1;
                var endHour = int.TryParse(_configuration["AutonomousLearning:EndHour"], out var eh) ? eh : 5;

                if (!IsInWindow(now, startHour, endHour))
                {
                    _logger.LogDebug("Outside learning window ({Start}:00-{End}:00)", startHour, endHour);
                    continue;
                }

                var minHoursBetweenRuns = int.TryParse(_configuration["AutonomousLearning:MinHoursBetweenRuns"], out var mh) ? mh : 12;
                if (_lastRun != DateTime.MinValue && (now - _lastRun).TotalHours < minHoursBetweenRuns)
                {
                    _logger.LogDebug("Learning run skipped (cooldown)");
                    continue;
                }

                if (!await _learningService.ShouldTriggerLearningCycleAsync())
                {
                    _logger.LogInformation("Not enough data for learning cycle");
                    continue;
                }

                _logger.LogInformation("Starting learning cycle");
                _lastRun = now;
                await _learningService.ExecuteLearningCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Learning scheduler error");
            }
        }

        _logger.LogInformation("Learning Scheduler stopped");
    }

    private static bool IsInWindow(DateTime now, int startHour, int endHour)
    {
        var hour = now.Hour;
        if (startHour > endHour)
        {
            return hour >= startHour || hour < endHour;
        }
        return hour >= startHour && hour < endHour;
    }
}
