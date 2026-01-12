using AI_Bible_App.Core.Models;
using System.Text.Json;

namespace AI_Bible_App.Infrastructure.Repositories;

/// <summary>
/// Repository for storing and managing training data for future model fine-tuning
/// </summary>
public class TrainingDataRepository : ITrainingDataRepository
{
    private readonly string _dataPath;
    private readonly string _conversationsFile;

    public TrainingDataRepository()
    {
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIBibleApp",
            "TrainingData");
        
        _conversationsFile = Path.Combine(_dataPath, "training_conversations.jsonl");
        
        Directory.CreateDirectory(_dataPath);
    }

    public async Task SaveTrainingConversationAsync(TrainingConversation conversation)
    {
        try
        {
            var json = JsonSerializer.Serialize(conversation);
            await File.AppendAllLinesAsync(_conversationsFile, new[] { json });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrainingData] Error saving: {ex.Message}");
        }
    }

    public async Task<List<TrainingConversation>> GetHighQualityConversationsAsync(double minScore = 0.7)
    {
        if (!File.Exists(_conversationsFile))
            return new List<TrainingConversation>();

        var conversations = new List<TrainingConversation>();

        try
        {
            var lines = await File.ReadAllLinesAsync(_conversationsFile);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var conversation = JsonSerializer.Deserialize<TrainingConversation>(line);
                if (conversation != null && conversation.QualityScore >= minScore)
                {
                    conversations.Add(conversation);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrainingData] Error loading: {ex.Message}");
        }

        return conversations;
    }

    public async Task<int> GetTotalConversationCountAsync()
    {
        if (!File.Exists(_conversationsFile))
            return 0;

        try
        {
            var lines = await File.ReadAllLinesAsync(_conversationsFile);
            return lines.Count(l => !string.IsNullOrWhiteSpace(l));
        }
        catch
        {
            return 0;
        }
    }

    public async Task ExportTrainingDataAsync(string outputPath)
    {
        var conversations = await GetHighQualityConversationsAsync(0.7);

        // Export in format suitable for fine-tuning (OpenAI, Unsloth, etc.)
        var exportData = conversations.Select(c => new
        {
            messages = c.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content
            }).ToList(),
            metadata = new
            {
                character = c.CharacterName,
                topic = c.Topic,
                quality_score = c.QualityScore,
                source = c.Source.ToString(),
                created_at = c.CreatedAt
            }
        });

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        await File.WriteAllTextAsync(outputPath, json);
    }
}
