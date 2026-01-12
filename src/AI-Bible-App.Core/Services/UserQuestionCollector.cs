using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Services;

/// <summary>
/// Collects anonymized user questions for improving training data
/// Only stores questions user explicitly opts-in to share
/// </summary>
public interface IUserQuestionCollector
{
    Task SaveUserQuestionAsync(string question, string characterId, bool userConsented);
    Task<List<string>> GetUniqueQuestionsAsync(int minOccurrences = 2);
    Task<Dictionary<string, int>> GetQuestionFrequencyAsync();
}

public class UserQuestionCollector : IUserQuestionCollector
{
    private readonly string _dataPath;
    private readonly string _questionsFile;

    public UserQuestionCollector()
    {
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIBibleApp",
            "UserQuestions");
        
        _questionsFile = Path.Combine(_dataPath, "user_questions.txt");
        
        Directory.CreateDirectory(_dataPath);
    }

    public async Task SaveUserQuestionAsync(string question, string characterId, bool userConsented)
    {
        if (!userConsented || string.IsNullOrWhiteSpace(question))
            return;

        try
        {
            // Anonymize and sanitize
            var sanitized = SanitizeQuestion(question);
            var entry = $"{DateTime.UtcNow:yyyy-MM-dd}|{characterId}|{sanitized}";
            
            await File.AppendAllLinesAsync(_questionsFile, new[] { entry });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuestionCollector] Error saving: {ex.Message}");
        }
    }

    public async Task<List<string>> GetUniqueQuestionsAsync(int minOccurrences = 2)
    {
        if (!File.Exists(_questionsFile))
            return new List<string>();

        try
        {
            var lines = await File.ReadAllLinesAsync(_questionsFile);
            var questionCounts = new Dictionary<string, int>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('|');
                if (parts.Length < 3) continue;

                var question = parts[2];
                questionCounts[question] = questionCounts.GetValueOrDefault(question, 0) + 1;
            }

            return questionCounts
                .Where(kvp => kvp.Value >= minOccurrences)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<Dictionary<string, int>> GetQuestionFrequencyAsync()
    {
        if (!File.Exists(_questionsFile))
            return new Dictionary<string, int>();

        try
        {
            var lines = await File.ReadAllLinesAsync(_questionsFile);
            var frequency = new Dictionary<string, int>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('|');
                if (parts.Length < 3) continue;

                var question = parts[2];
                frequency[question] = frequency.GetValueOrDefault(question, 0) + 1;
            }

            return frequency;
        }
        catch
        {
            return new Dictionary<string, int>();
        }
    }

    private string SanitizeQuestion(string question)
    {
        // Remove personally identifiable information
        question = System.Text.RegularExpressions.Regex.Replace(question, @"\b\d{3}-\d{3}-\d{4}\b", "[phone]");
        question = System.Text.RegularExpressions.Regex.Replace(question, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[email]");
        question = System.Text.RegularExpressions.Regex.Replace(question, @"\b\d{5}(?:-\d{4})?\b", "[zipcode]");
        
        // Remove specific names (common first names)
        var commonNames = new[] { "John", "Mary", "Michael", "Jennifer", "David", "Lisa", "James", "Sarah", "Robert", "Jessica" };
        foreach (var name in commonNames)
        {
            question = System.Text.RegularExpressions.Regex.Replace(question, $@"\b{name}\b", "[name]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return question.Trim();
    }
}
