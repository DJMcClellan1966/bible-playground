using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Repositories;

/// <summary>
/// JSON file-based implementation of chat repository with encryption.
/// Inherits common functionality from JsonRepositoryBase.
/// </summary>
public class JsonChatRepository : JsonRepositoryBase<ChatSession>, IChatRepository
{
    public JsonChatRepository(
        ILogger<JsonChatRepository> logger,
        IEncryptionService? encryptionService = null,
        IFileSecurityService? fileSecurityService = null,
        string dataDirectory = "data")
        : base(logger, "chat_sessions.json", encryptionService, fileSecurityService, dataDirectory)
    {
    }

    protected override string GetEntityId(ChatSession entity) => entity.Id;
    protected override string EntityTypeName => "chat session";

    public Task<ChatSession> GetSessionAsync(string sessionId)
        => GetByIdAsync(sessionId, $"Session {sessionId} not found");

    public Task<List<ChatSession>> GetAllSessionsAsync()
        => LoadAllAsync();

    public async Task<List<ChatSession>> GetAllSessionsForUserAsync(string userId)
    {
        var sessions = await LoadAllAsync();
        return sessions.Where(s => s.UserId == userId).ToList();
    }

    public Task SaveSessionAsync(ChatSession session)
        => UpsertAsync(session, session.Id);

    public Task DeleteSessionAsync(string sessionId)
        => DeleteByIdAsync(sessionId);

    public async Task<ChatSession?> GetLatestSessionForCharacterAsync(string characterId)
    {
        var sessions = await LoadAllAsync();
        return sessions
            .Where(s => s.CharacterId == characterId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();
    }
}
