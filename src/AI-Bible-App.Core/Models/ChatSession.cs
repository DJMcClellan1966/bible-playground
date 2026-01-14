namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents a chat conversation session with biblical character(s)
/// </summary>
public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public string CharacterId { get; set; } = string.Empty;
    
    /// <summary>
    /// List of character IDs participating in this session (for roundtable discussions)
    /// </summary>
    public List<string> ParticipantCharacterIds { get; set; } = new();
    
    /// <summary>
    /// Type of chat session
    /// </summary>
    public ChatSessionType SessionType { get; set; } = ChatSessionType.SingleCharacter;
    
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    
    /// <summary>
    /// User IDs this chat has been shared with (for family/group study)
    /// </summary>
    public List<string> SharedWithUserIds { get; set; } = new();
    
    /// <summary>
    /// Whether this chat is shared with all users on the device
    /// </summary>
    public bool IsSharedWithAll { get; set; } = false;
    
    /// <summary>
    /// Tracks conversation themes and topics for memory/growth tracking
    /// </summary>
    public List<string> DiscussedThemes { get; set; } = new();
    
    /// <summary>
    /// Story ID if this is an interactive story session
    /// </summary>
    public string? StoryId { get; set; }
    
    /// <summary>
    /// Character emoji for display (cached from character data)
    /// </summary>
    public string CharacterEmoji { get; set; } = "👤";
    
    /// <summary>
    /// Character name for display (cached from character data)
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;
    
    /// <summary>
    /// Preview of the last message in this chat session
    /// </summary>
    public string LastMessagePreview { get; set; } = string.Empty;

    // Devil's Advocate session settings
    public bool DevilsAdvocateEnabled { get; set; } = false;
    public string AdvocateTone { get; set; } = "soft"; // soft, firm, strong
    public List<string> ContrarianCharacterIds { get; set; } = new();
}

/// <summary>
/// Types of chat sessions
/// </summary>
public enum ChatSessionType
{
    SingleCharacter,
    Roundtable,
    WisdomCouncil,
    InteractiveStory,
    PrayerChain
}
