using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Interface for AI service interactions
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Sends a chat message and gets a response from the AI
    /// </summary>
    Task<string> GetChatResponseAsync(BiblicalCharacter character, List<ChatMessage> conversationHistory, string userMessage, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a chat message and streams response tokens in real-time
    /// </summary>
    IAsyncEnumerable<string> StreamChatResponseAsync(BiblicalCharacter character, List<ChatMessage> conversationHistory, string userMessage, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a prayer based on the given topic
    /// </summary>
    Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a personalized prayer with detailed options
    /// </summary>
    Task<string> GeneratePersonalizedPrayerAsync(PrayerOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a daily devotional with structured content
    /// </summary>
    Task<string> GenerateDevotionalAsync(DateTime date, CancellationToken cancellationToken = default);
}
