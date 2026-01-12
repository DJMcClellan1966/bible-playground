using AI_Bible_App.Core.Models;
using AI_Bible_App.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Orchestrates the autonomous learning cycle:
/// 1. Collect high-quality training data
/// 2. Fine-tune model
/// 3. Evaluate new model
/// 4. Deploy if improved
/// </summary>
public class AutonomousLearningService : IAutonomousLearningService
{
    private readonly ITrainingDataRepository _trainingRepo;
    private readonly IModelFineTuningService _fineTuneService;
    private readonly IModelEvaluationService _evaluationService;
    private readonly ILogger<AutonomousLearningService> _logger;
    private readonly string _dataDirectory;
    
    // Learning cycle configuration
    private const int MIN_CONVERSATIONS_FOR_CYCLE = 100;
    private const double MIN_QUALITY_SCORE = 4.0;
    private const double MIN_IMPROVEMENT_FOR_DEPLOYMENT = 0.03; // 3% improvement
    private const int MAX_CONVERSATIONS_PER_CYCLE = 1000;
    
    public AutonomousLearningService(
        ITrainingDataRepository trainingRepo,
        IModelFineTuningService fineTuneService,
        IModelEvaluationService evaluationService,
        ILogger<AutonomousLearningService> logger)
    {
        _trainingRepo = trainingRepo;
        _fineTuneService = fineTuneService;
        _evaluationService = evaluationService;
        _logger = logger;
        
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIBibleApp",
            "AutonomousLearning");
        
        Directory.CreateDirectory(_dataDirectory);
    }
    
    public async Task<LearningCycleResult> ExecuteLearningCycleAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new LearningCycleResult
        {
            StartedAt = DateTime.UtcNow
        };
        
        try
        {
            _logger.LogInformation("Starting autonomous learning cycle");
            
            // Step 1: Get current model version
            result.PreviousModelVersion = await GetCurrentModelVersionAsync();
            _logger.LogInformation("Current model version: {Version}", result.PreviousModelVersion);
            
            // Step 2: Collect high-quality training data
            _logger.LogInformation("Collecting training data (min quality: {MinQuality})", MIN_QUALITY_SCORE);
            var conversations = await _trainingRepo.GetHighQualityConversationsAsync(
                MIN_QUALITY_SCORE,
                MAX_CONVERSATIONS_PER_CYCLE);
            
            result.ConversationsUsed = conversations.Count;
            
            if (conversations.Count < MIN_CONVERSATIONS_FOR_CYCLE)
            {
                result.Success = false;
                result.Errors.Add($"Insufficient training data: {conversations.Count} conversations (need {MIN_CONVERSATIONS_FOR_CYCLE})");
                _logger.LogWarning("Not enough training data for learning cycle");
                return result;
            }
            
            _logger.LogInformation("Collected {Count} high-quality conversations for training", conversations.Count);
            
            // Step 3: Export training data
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var trainingDataPath = Path.Combine(_dataDirectory, $"training-{timestamp}.jsonl");
            await _trainingRepo.ExportTrainingDataAsync(trainingDataPath, conversations);
            _logger.LogInformation("Exported training data to {Path}", trainingDataPath);
            
            // Step 4: Start fine-tuning
            _logger.LogInformation("Starting fine-tuning job");
            var fineTuneConfig = new FineTuningConfig
            {
                BaseModel = "phi4:latest",
                Epochs = 3,
                LearningRate = 2e-5,
                BatchSize = 4,
                UseLoRA = true,
                LoRARank = 16
            };
            
            var fineTuneJob = await _fineTuneService.StartFineTuningAsync(
                trainingDataPath,
                fineTuneConfig,
                cancellationToken);
            
            // Wait for completion (with timeout)
            var timeout = TimeSpan.FromHours(4);
            var startTime = DateTime.UtcNow;
            
            while (true)
            {
                var status = await _fineTuneService.GetJobStatusAsync(fineTuneJob.JobId);
                
                if (status.Status == "completed")
                {
                    result.NewModelVersion = $"v{timestamp}";
                    _logger.LogInformation("Fine-tuning completed: {Version}", result.NewModelVersion);
                    break;
                }
                
                if (status.Status == "failed")
                {
                    result.Success = false;
                    result.Errors.Add($"Fine-tuning failed: {status.ErrorMessage}");
                    _logger.LogError("Fine-tuning failed: {Error}", status.ErrorMessage);
                    return result;
                }
                
                if (DateTime.UtcNow - startTime > timeout)
                {
                    await _fineTuneService.CancelJobAsync(fineTuneJob.JobId);
                    result.Success = false;
                    result.Errors.Add("Fine-tuning timeout");
                    _logger.LogError("Fine-tuning timeout after {Hours} hours", timeout.TotalHours);
                    return result;
                }
                
                _logger.LogInformation("Fine-tuning progress: {Progress:P0}", status.Progress);
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
            
            // Step 5: Get fine-tuned model path
            var newModelPath = await _fineTuneService.GetFineTunedModelPathAsync(fineTuneJob.JobId);
            
            if (string.IsNullOrEmpty(newModelPath))
            {
                result.Success = false;
                result.Errors.Add("Could not locate fine-tuned model");
                _logger.LogError("Fine-tuned model path not found");
                return result;
            }
            
            // Step 6: Evaluate new model vs baseline
            _logger.LogInformation("Evaluating new model");
            var currentModelPath = GetCurrentModelPath();
            var evaluation = await _evaluationService.EvaluateModelAsync(
                newModelPath,
                currentModelPath,
                cancellationToken);
            
            result.ImprovementScore = evaluation.ImprovementVsBaseline;
            _logger.LogInformation("Model evaluation: {Score:F3} (improvement: {Improvement:+F3})",
                evaluation.OverallScore, evaluation.ImprovementVsBaseline);
            
            // Step 7: Deploy if improved significantly
            if (evaluation.ImprovementVsBaseline >= MIN_IMPROVEMENT_FOR_DEPLOYMENT)
            {
                await DeployNewModelAsync(newModelPath, result.NewModelVersion);
                result.ModelDeployed = true;
                result.DeploymentMessage = $"Model deployed with {evaluation.ImprovementVsBaseline:+P1} improvement";
                _logger.LogInformation("New model deployed: {Version} (+{Improvement:P1})",
                    result.NewModelVersion, evaluation.ImprovementVsBaseline);
            }
            else
            {
                result.ModelDeployed = false;
                result.DeploymentMessage = $"Model not deployed - insufficient improvement ({evaluation.ImprovementVsBaseline:+P1}, need {MIN_IMPROVEMENT_FOR_DEPLOYMENT:P1})";
                _logger.LogInformation("Model not deployed due to insufficient improvement");
            }
            
            // Step 8: Save learning cycle history
            await SaveLearningCycleResultAsync(result);
            
            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Learning cycle completed successfully in {Duration}",
                result.CompletedAt - result.StartedAt);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Exception: {ex.Message}");
            result.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Learning cycle failed");
        }
        
        return result;
    }
    
    public async Task<bool> ShouldTriggerLearningCycleAsync()
    {
        try
        {
            // Check if enough new conversations exist
            var conversations = await _trainingRepo.GetHighQualityConversationsAsync(
                MIN_QUALITY_SCORE,
                MIN_CONVERSATIONS_FOR_CYCLE + 1);
            
            if (conversations.Count < MIN_CONVERSATIONS_FOR_CYCLE)
            {
                return false;
            }
            
            // Check if enough time has passed since last cycle
            var stats = await GetLearningStatisticsAsync();
            if (stats.LastLearningCycle.HasValue)
            {
                var daysSinceLastCycle = (DateTime.UtcNow - stats.LastLearningCycle.Value).TotalDays;
                if (daysSinceLastCycle < 7) // At least weekly
                {
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if learning cycle should trigger");
            return false;
        }
    }
    
    public async Task<LearningStatistics> GetLearningStatisticsAsync()
    {
        var statsPath = Path.Combine(_dataDirectory, "learning_statistics.json");
        
        if (!File.Exists(statsPath))
        {
            return new LearningStatistics
            {
                CurrentModelVersion = "base"
            };
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(statsPath);
            return JsonSerializer.Deserialize<LearningStatistics>(json) ?? new LearningStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading learning statistics");
            return new LearningStatistics();
        }
    }
    
    public async Task<string> GetCurrentModelVersionAsync()
    {
        var stats = await GetLearningStatisticsAsync();
        return stats.CurrentModelVersion;
    }
    
    private string GetCurrentModelPath()
    {
        // This would return the path to the currently deployed model
        // For now, assume base model
        return "phi4:latest";
    }
    
    private async Task DeployNewModelAsync(string modelPath, string version)
    {
        // Copy model to deployment location
        var deployPath = Path.Combine(_dataDirectory, "deployed_models", version);
        Directory.CreateDirectory(deployPath);
        
        // In production, this would:
        // 1. Copy model files
        // 2. Update configuration
        // 3. Restart AI service (or hot-swap model)
        
        _logger.LogInformation("Deployed model {Version} to {Path}", version, deployPath);
        await Task.CompletedTask;
    }
    
    private async Task SaveLearningCycleResultAsync(LearningCycleResult result)
    {
        // Update statistics
        var stats = await GetLearningStatisticsAsync();
        stats.TotalLearningCycles++;
        if (result.ModelDeployed)
        {
            stats.SuccessfulDeployments++;
            stats.CurrentModelVersion = result.NewModelVersion;
        }
        stats.LastLearningCycle = result.CompletedAt;
        stats.TotalConversationsUsedForTraining += result.ConversationsUsed;
        
        if (result.ModelDeployed)
        {
            stats.VersionHistory.Add(new ModelVersionHistory
            {
                Version = result.NewModelVersion,
                DeployedAt = result.CompletedAt,
                ImprovementScore = result.ImprovementScore,
                ConversationsUsed = result.ConversationsUsed
            });
        }
        
        // Calculate average improvement
        if (stats.VersionHistory.Any())
        {
            stats.AverageImprovementPerCycle = stats.VersionHistory.Average(v => v.ImprovementScore);
        }
        
        var statsPath = Path.Combine(_dataDirectory, "learning_statistics.json");
        var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(statsPath, json);
        
        // Also save detailed result
        var resultPath = Path.Combine(_dataDirectory, $"cycle-{result.StartedAt:yyyyMMdd-HHmmss}.json");
        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(resultPath, resultJson);
    }
}
