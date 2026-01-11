namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents all syncable data for a user - used for cloud sync operations
/// </summary>
public class UserSyncData
{
    /// <summary>
    /// Version of the sync format (for future migrations)
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
    
    /// <summary>
    /// Cloud-side user ID
    /// </summary>
    public string CloudUserId { get; set; } = string.Empty;
    
    /// <summary>
    /// User profile information
    /// </summary>
    public SyncedUserProfile Profile { get; set; } = new();
    
    /// <summary>
    /// Character memories (what characters have learned about this user)
    /// </summary>
    public List<UserCharacterMemory> CharacterMemories { get; set; } = new();
    
    /// <summary>
    /// Chat sessions (conversation history)
    /// </summary>
    public List<ChatSession> ChatSessions { get; set; } = new();
    
    /// <summary>
    /// Saved prayers
    /// </summary>
    public List<SavedPrayer> Prayers { get; set; } = new();
    
    /// <summary>
    /// Journal reflections
    /// </summary>
    public List<Reflection> Reflections { get; set; } = new();
    
    /// <summary>
    /// Bookmarked verses
    /// </summary>
    public List<VerseBookmark> Bookmarks { get; set; } = new();
    
    /// <summary>
    /// When this data was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Which device last modified this data
    /// </summary>
    public string? LastModifiedByDevice { get; set; }
}

/// <summary>
/// User profile data that syncs across devices
/// </summary>
public class SyncedUserProfile
{
    public string Name { get; set; } = string.Empty;
    public string? AvatarEmoji { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserSettings Settings { get; set; } = new();
}

// Note: VerseBookmark class is already defined in VerseBookmark.cs
