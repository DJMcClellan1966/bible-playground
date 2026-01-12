namespace AI_Bible_App.Core.Services;

/// <summary>
/// Service for autonomous model improvement through continuous learning cycles
/// </summary>
public interface IAutonomousLearningService
{
    /// <summary>
    /// Trigger a complete learning cycle: collect data, fine-tune, evaluate, deploy
    /// </summary>
    Task<LearningCycleResult> ExecuteLearningCycleAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if enough new high-quality data exists to warrant a learning cycle
    /// </summary>
    Task<bool> ShouldTriggerLearningCycleAsync();
    
    /// <summary>
    /// Get statistics about learning cycles and model improvements
    /// </summary>
    Task<LearningStatistics> GetLearningStatisticsAsync();
    
    /// <summary>
    /// Get the current active model version
    /// </summary>
    Task<string> GetCurrentModelVersionAsync();
}

public class LearningCycleResult
{
    public bool Success { get; set; }
    public string NewModelVersion { get; set; } = string.Empty;
    public string? PreviousModelVersion { get; set; }
    public double ImprovementScore { get; set; }
    public bool ModelDeployed { get; set; }
    public int ConversationsUsed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? DeploymentMessage { get; set; }
}

public class LearningStatistics
{
    public int TotalLearningCycles { get; set; }
    public int SuccessfulDeployments { get; set; }
    public DateTime? LastLearningCycle { get; set; }
    public string CurrentModelVersion { get; set; } = "base";
    public int TotalConversationsUsedForTraining { get; set; }
    public double AverageImprovementPerCycle { get; set; }
    public List<ModelVersionHistory> VersionHistory { get; set; } = new();
}

public class ModelVersionHistory
{
    public string Version { get; set; } = string.Empty;
    public DateTime DeployedAt { get; set; }
    public double ImprovementScore { get; set; }
    public int ConversationsUsed { get; set; }
    public string? Notes { get; set; }
}
