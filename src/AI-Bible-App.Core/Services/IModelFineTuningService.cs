namespace AI_Bible_App.Core.Services;

/// <summary>
/// Service for fine-tuning language models with collected training data
/// </summary>
public interface IModelFineTuningService
{
    /// <summary>
    /// Start a fine-tuning job with the provided training data
    /// </summary>
    Task<FineTuningJob> StartFineTuningAsync(
        string trainingDataPath,
        FineTuningConfig config,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check the status of a fine-tuning job
    /// </summary>
    Task<FineTuningJobStatus> GetJobStatusAsync(string jobId);
    
    /// <summary>
    /// Cancel a running fine-tuning job
    /// </summary>
    Task CancelJobAsync(string jobId);
    
    /// <summary>
    /// Get the path to the fine-tuned model once complete
    /// </summary>
    Task<string?> GetFineTunedModelPathAsync(string jobId);
}

public class FineTuningJob
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime StartedAt { get; set; }
    public string BaseModel { get; set; } = string.Empty;
    public string TrainingDataPath { get; set; } = string.Empty;
    public FineTuningConfig Config { get; set; } = new();
}

public class FineTuningConfig
{
    /// <summary>
    /// Base model to fine-tune (e.g., "phi4:latest")
    /// </summary>
    public string BaseModel { get; set; } = "phi4:latest";
    
    /// <summary>
    /// Number of training epochs
    /// </summary>
    public int Epochs { get; set; } = 3;
    
    /// <summary>
    /// Learning rate
    /// </summary>
    public double LearningRate { get; set; } = 2e-5;
    
    /// <summary>
    /// Batch size for training
    /// </summary>
    public int BatchSize { get; set; } = 4;
    
    /// <summary>
    /// Maximum sequence length
    /// </summary>
    public int MaxSequenceLength { get; set; } = 2048;
    
    /// <summary>
    /// Whether to use LoRA (Low-Rank Adaptation) for efficient fine-tuning
    /// </summary>
    public bool UseLoRA { get; set; } = true;
    
    /// <summary>
    /// LoRA rank (lower = fewer parameters, faster training)
    /// </summary>
    public int LoRARank { get; set; } = 16;
    
    /// <summary>
    /// LoRA alpha parameter
    /// </summary>
    public double LoRAAlpha { get; set; } = 32.0;
    
    /// <summary>
    /// Dropout rate for LoRA
    /// </summary>
    public double LoRADropout { get; set; } = 0.05;
}

public class FineTuningJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown"; // pending, running, completed, failed, cancelled
    public double Progress { get; set; } // 0.0 to 1.0
    public int CurrentEpoch { get; set; }
    public int TotalEpochs { get; set; }
    public double? CurrentLoss { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ModelPath { get; set; }
}
