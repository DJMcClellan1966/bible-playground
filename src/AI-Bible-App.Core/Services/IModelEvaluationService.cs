namespace AI_Bible_App.Core.Services;

/// <summary>
/// Service for evaluating model quality and comparing model versions
/// </summary>
public interface IModelEvaluationService
{
    /// <summary>
    /// Evaluate a model using test questions and compare to baseline
    /// </summary>
    Task<ModelEvaluationResult> EvaluateModelAsync(
        string modelPath,
        string? baselineModelPath = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get evaluation test questions (separate from training data)
    /// </summary>
    Task<List<EvaluationQuestion>> GetEvaluationQuestionsAsync();
    
    /// <summary>
    /// Score a single response for quality
    /// </summary>
    Task<ResponseQualityScore> ScoreResponseAsync(
        string question,
        string response,
        string characterId);
}

public class ModelEvaluationResult
{
    public string ModelPath { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    
    // Overall scores
    public double OverallScore { get; set; } // 0.0 to 1.0
    public double ImprovementVsBaseline { get; set; } // -1.0 to 1.0
    
    // Detailed metrics
    public double RelevanceScore { get; set; } // How well responses address questions
    public double CharacterConsistencyScore { get; set; } // How well character voice is maintained
    public double BiblicalAccuracyScore { get; set; } // Correctness of biblical references
    public double InsightfulnessScore { get; set; } // Depth of insights
    public double RepetitionScore { get; set; } // Penalize repetitive responses
    
    // Per-character breakdown
    public Dictionary<string, double> CharacterScores { get; set; } = new();
    
    // Sample evaluations
    public List<SampleEvaluation> SampleEvaluations { get; set; } = new();
    
    public string? Notes { get; set; }
}

public class EvaluationQuestion
{
    public string Question { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ExpectedThemes { get; set; } // Themes that should appear in response
    public double Weight { get; set; } = 1.0; // Some questions more important than others
}

public class SampleEvaluation
{
    public string Question { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public ResponseQualityScore Score { get; set; } = new();
}

public class ResponseQualityScore
{
    public double OverallScore { get; set; }
    public double Relevance { get; set; }
    public double CharacterVoice { get; set; }
    public double BiblicalAccuracy { get; set; }
    public double Insightfulness { get; set; }
    public double Conciseness { get; set; }
    public List<string> PositiveAspects { get; set; } = new();
    public List<string> ImprovementAreas { get; set; } = new();
}
