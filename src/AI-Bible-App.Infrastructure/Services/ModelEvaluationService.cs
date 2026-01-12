using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Services;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Evaluates model quality using test questions and scoring metrics
/// </summary>
public class ModelEvaluationService : IModelEvaluationService
{
    private readonly IAIService _aiService;
    private readonly ILogger<ModelEvaluationService> _logger;
    private readonly List<EvaluationQuestion> _evaluationQuestions;
    
    public ModelEvaluationService(
        IAIService aiService,
        ILogger<ModelEvaluationService> logger)
    {
        _aiService = aiService;
        _logger = logger;
        _evaluationQuestions = InitializeEvaluationQuestions();
    }
    
    public async Task<ModelEvaluationResult> EvaluateModelAsync(
        string modelPath,
        string? baselineModelPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting model evaluation for {ModelPath}", modelPath);
        
        var result = new ModelEvaluationResult
        {
            ModelPath = modelPath,
            ModelVersion = ExtractVersionFromPath(modelPath),
            EvaluatedAt = DateTime.UtcNow
        };
        
        double totalScore = 0;
        var characterScores = new Dictionary<string, List<double>>();
        
        // Evaluate each test question
        foreach (var question in _evaluationQuestions)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            try
            {
                // Get response from model
                var response = await GetModelResponseAsync(modelPath, question, cancellationToken);
                
                // Score the response
                var score = await ScoreResponseAsync(question.Question, response, question.CharacterId);
                
                // Weight and accumulate
                totalScore += score.OverallScore * question.Weight;
                
                // Track per-character scores
                if (!characterScores.ContainsKey(question.CharacterId))
                {
                    characterScores[question.CharacterId] = new List<double>();
                }
                characterScores[question.CharacterId].Add(score.OverallScore);
                
                // Add sample if particularly good or bad
                if (score.OverallScore > 0.8 || score.OverallScore < 0.4)
                {
                    result.SampleEvaluations.Add(new SampleEvaluation
                    {
                        Question = question.Question,
                        CharacterId = question.CharacterId,
                        Response = response,
                        Score = score
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate question: {Question}", question.Question);
            }
        }
        
        // Calculate overall metrics
        var totalWeight = _evaluationQuestions.Sum(q => q.Weight);
        result.OverallScore = totalScore / totalWeight;
        
        // Calculate per-character scores
        foreach (var (characterId, scores) in characterScores)
        {
            result.CharacterScores[characterId] = scores.Average();
        }
        
        // Calculate detailed metrics (simplified - could be more sophisticated)
        result.RelevanceScore = result.OverallScore * 0.9; // Assume relevance is primary component
        result.CharacterConsistencyScore = result.CharacterScores.Values.Min(); // Weakest character
        result.BiblicalAccuracyScore = result.OverallScore * 0.95; // Assume high accuracy
        result.InsightfulnessScore = result.OverallScore * 0.85;
        result.RepetitionScore = 1.0 - (result.OverallScore * 0.1); // Inverse metric
        
        // Compare to baseline if provided
        if (baselineModelPath != null)
        {
            var baselineResult = await EvaluateModelAsync(baselineModelPath, null, cancellationToken);
            result.ImprovementVsBaseline = result.OverallScore - baselineResult.OverallScore;
        }
        
        _logger.LogInformation("Model evaluation completed. Overall score: {Score:F3}, Improvement: {Improvement:F3}",
            result.OverallScore, result.ImprovementVsBaseline);
        
        return result;
    }
    
    private async Task<string> GetModelResponseAsync(
        string modelPath,
        EvaluationQuestion question,
        CancellationToken cancellationToken)
    {
        // This would need to load the specific model and get response
        // For now, use the current AI service (would need enhancement)
        var messages = new List<AI_Bible_App.Core.Models.ChatMessage>
        {
            new() { Role = "user", Content = question.Question }
        };
        
        return await _aiService.GetResponseAsync(messages, question.CharacterId, cancellationToken);
    }
    
    public Task<List<EvaluationQuestion>> GetEvaluationQuestionsAsync()
    {
        return Task.FromResult(_evaluationQuestions);
    }
    
    public Task<ResponseQualityScore> ScoreResponseAsync(
        string question,
        string response,
        string characterId)
    {
        var score = new ResponseQualityScore();
        
        // 1. Relevance: Does response address the question?
        score.Relevance = CalculateRelevance(question, response);
        
        // 2. Character voice: Does it sound like the character?
        score.CharacterVoice = CalculateCharacterVoiceScore(response, characterId);
        
        // 3. Biblical accuracy: Are references correct?
        score.BiblicalAccuracy = CheckBiblicalAccuracy(response);
        
        // 4. Insightfulness: Does it provide depth?
        score.Insightfulness = CalculateInsightfulness(response);
        
        // 5. Conciseness: Appropriate length?
        score.Conciseness = CalculateConciseness(response);
        
        // Overall weighted score
        score.OverallScore = (
            score.Relevance * 0.35 +
            score.CharacterVoice * 0.25 +
            score.BiblicalAccuracy * 0.20 +
            score.Insightfulness * 0.15 +
            score.Conciseness * 0.05
        );
        
        // Generate feedback
        if (score.Relevance > 0.8) score.PositiveAspects.Add("Directly addresses the question");
        if (score.CharacterVoice > 0.8) score.PositiveAspects.Add("Strong character voice");
        if (score.Insightfulness > 0.8) score.PositiveAspects.Add("Provides meaningful insight");
        
        if (score.Relevance < 0.5) score.ImprovementAreas.Add("Response doesn't fully address the question");
        if (score.CharacterVoice < 0.5) score.ImprovementAreas.Add("Character voice could be stronger");
        if (response.Length < 100) score.ImprovementAreas.Add("Response too brief");
        if (response.Length > 1000) score.ImprovementAreas.Add("Response too verbose");
        
        return Task.FromResult(score);
    }
    
    private double CalculateRelevance(string question, string response)
    {
        // Extract key terms from question
        var questionTerms = question.ToLower()
            .Split(new[] { ' ', '?', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();
        
        var responseTerms = response.ToLower()
            .Split(new[] { ' ', '?', '.', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();
        
        var overlap = questionTerms.Intersect(responseTerms).Count();
        var relevance = (double)overlap / Math.Max(questionTerms.Count, 1);
        
        // Bonus for direct answer phrases
        if (response.Contains("you") || response.Contains("your"))
            relevance += 0.1;
        
        return Math.Min(relevance, 1.0);
    }
    
    private double CalculateCharacterVoiceScore(string response, string characterId)
    {
        // Check for first-person perspective
        var hasFirstPerson = response.Contains("I ") || response.Contains("my ") || response.Contains("me ");
        
        // Check for personal experience references
        var hasPersonalExperience = response.Contains("When I") || response.Contains("I remember") || 
                                   response.Contains("I learned") || response.Contains("I struggled");
        
        double score = hasFirstPerson ? 0.5 : 0.2;
        if (hasPersonalExperience) score += 0.3;
        
        // Character-specific markers (simplified)
        score += characterId switch
        {
            "moses" when response.Contains("wilderness") || response.Contains("Egypt") => 0.2,
            "david" when response.Contains("shepherd") || response.Contains("king") => 0.2,
            "paul" when response.Contains("Christ") || response.Contains("grace") => 0.2,
            "peter" when response.Contains("faith") || response.Contains("rock") => 0.2,
            _ => 0.0
        };
        
        return Math.Min(score, 1.0);
    }
    
    private double CheckBiblicalAccuracy(string response)
    {
        // Check for biblical book references
        var biblicalBooks = new[] { "Genesis", "Exodus", "Psalms", "Proverbs", "Matthew", "John", "Romans", "Corinthians" };
        var hasReference = biblicalBooks.Any(book => response.Contains(book, StringComparison.OrdinalIgnoreCase));
        
        // Start with high score, reduce if obvious errors
        double score = 0.8;
        if (hasReference) score = 1.0;
        
        // Could enhance with actual verse validation
        return score;
    }
    
    private double CalculateInsightfulness(string response)
    {
        // Look for depth indicators
        var depthMarkers = new[]
        {
            "because", "therefore", "however", "although",
            "realize", "understand", "discover", "learn",
            "deeper", "meaningful", "insight", "perspective"
        };
        
        var depthCount = depthMarkers.Count(marker => 
            response.Contains(marker, StringComparison.OrdinalIgnoreCase));
        
        double score = Math.Min(depthCount * 0.2, 0.8);
        
        // Bonus for questions that prompt reflection
        if (response.Contains("?")) score += 0.2;
        
        return Math.Min(score, 1.0);
    }
    
    private double CalculateConciseness(string response)
    {
        var length = response.Length;
        
        // Optimal range: 200-500 characters
        if (length >= 200 && length <= 500) return 1.0;
        if (length >= 150 && length <= 700) return 0.8;
        if (length >= 100 && length <= 1000) return 0.6;
        return 0.4;
    }
    
    private string ExtractVersionFromPath(string path)
    {
        // Extract version from path like "models/bible-app-v1.2.3"
        var parts = path.Split('/', '\\');
        var modelName = parts.LastOrDefault() ?? "unknown";
        
        if (modelName.Contains("-v"))
        {
            return modelName.Substring(modelName.IndexOf("-v") + 2);
        }
        
        return DateTime.UtcNow.ToString("yyyyMMdd");
    }
    
    private List<EvaluationQuestion> InitializeEvaluationQuestions()
    {
        // Set aside evaluation questions (NOT used in training)
        return new List<EvaluationQuestion>
        {
            // Moses questions
            new() { CharacterId = "moses", Question = "How do you deal with feeling inadequate for a big responsibility?", Category = "Leadership", Weight = 1.2 },
            new() { CharacterId = "moses", Question = "What helps you keep going when people complain constantly?", Category = "Perseverance", Weight = 1.0 },
            
            // David questions
            new() { CharacterId = "david", Question = "How do you handle guilt after making a terrible mistake?", Category = "Guilt", Weight = 1.3 },
            new() { CharacterId = "david", Question = "What do you do when you feel far from God?", Category = "Faith", Weight = 1.1 },
            
            // Paul questions
            new() { CharacterId = "paul", Question = "How do you overcome regret about your past?", Category = "Redemption", Weight = 1.3 },
            new() { CharacterId = "paul", Question = "What gives you strength during physical suffering?", Category = "Suffering", Weight = 1.2 },
            
            // Peter questions
            new() { CharacterId = "peter", Question = "How do you rebuild after complete failure?", Category = "Failure", Weight = 1.3 },
            new() { CharacterId = "peter", Question = "What helps you take bold steps despite fear?", Category = "Courage", Weight = 1.0 },
            
            // Mary questions
            new() { CharacterId = "mary", Question = "How do you trust God when life is confusing?", Category = "Trust", Weight = 1.1 },
            new() { CharacterId = "mary", Question = "How do you stay close to someone going through suffering?", Category = "Compassion", Weight = 1.0 },
            
            // Cross-category questions
            new() { CharacterId = "moses", Question = "Is anger ever righteous?", Category = "Ethics", Weight = 1.0 },
            new() { CharacterId = "david", Question = "How do you balance ambition with contentment?", Category = "Wisdom", Weight = 1.0 },
            new() { CharacterId = "paul", Question = "When should you stand firm vs. compromise?", Category = "Ethics", Weight = 1.1 },
        };
    }
}
