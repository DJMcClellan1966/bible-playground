using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Repository for managing chat sessions
/// </summary>
public interface IChatRepository
{
    Task<ChatSession> GetSessionAsync(string sessionId);
    Task<List<ChatSession>> GetAllSessionsAsync();
    Task<List<ChatSession>> GetAllSessionsForUserAsync(string userId);
    Task<ChatSession?> GetLatestSessionForCharacterAsync(string characterId);
    Task SaveSessionAsync(ChatSession session);
    Task DeleteSessionAsync(string sessionId);
}
