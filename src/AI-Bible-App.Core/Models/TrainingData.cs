namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents conversation data that can be used for model fine-tuning
/// </summary>
public class TrainingConversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TrainingMessage> Messages { get; set; } = new();
    public double QualityScore { get; set; } = 0.0; // 0-1, human-rated or AI-evaluated
    public string Topic { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new(); // e.g., "faith", "suffering", "leadership"
    public bool IsHumanValidated { get; set; } = false;
    public ConversationSource Source { get; set; } = ConversationSource.RealUser;
}

public class TrainingMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? Rating { get; set; } // User rating if available
}

public enum ConversationSource
{
    RealUser,           // Actual user conversations (with consent)
    SyntheticGenerated, // AI-generated training examples
    CuratedExamples     // Hand-crafted by developers
}

/// <summary>
/// Repository for collecting training data
/// </summary>
public interface ITrainingDataRepository
{
    Task SaveTrainingConversationAsync(TrainingConversation conversation);
    Task<List<TrainingConversation>> GetHighQualityConversationsAsync(double minScore = 0.7);
    Task<int> GetTotalConversationCountAsync();
    Task ExportTrainingDataAsync(string outputPath); // Export to JSONL for fine-tuning
}
