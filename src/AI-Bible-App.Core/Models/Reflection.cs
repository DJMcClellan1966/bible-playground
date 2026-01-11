namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents a personal Bible reflection/journal entry
/// </summary>
public class Reflection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Title for the reflection entry
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of content saved (Chat, Prayer, BibleVerse, Custom)
    /// </summary>
    public ReflectionType Type { get; set; } = ReflectionType.Custom;
    
    /// <summary>
    /// The saved content (AI response, prayer text, or verse)
    /// </summary>
    public string SavedContent { get; set; } = string.Empty;
    
    /// <summary>
    /// User's personal thoughts/notes about the content
    /// </summary>
    public string PersonalNotes { get; set; } = string.Empty;
    
    /// <summary>
    /// Bible references related to this reflection
    /// </summary>
    public List<string> BibleReferences { get; set; } = new();
    
    /// <summary>
    /// The character who provided the response (if from chat)
    /// </summary>
    public string? CharacterName { get; set; }
    
    /// <summary>
    /// Tags for organizing reflections
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// Whether this is a favorite/starred reflection
    /// </summary>
    public bool IsFavorite { get; set; }
    
    /// <summary>
    /// User IDs this reflection has been shared with
    /// </summary>
    public List<string> SharedWithUserIds { get; set; } = new();
    
    /// <summary>
    /// Whether this reflection is shared with all users on the device
    /// </summary>
    public bool IsSharedWithAll { get; set; } = false;
}

public enum ReflectionType
{
    Custom,
    Chat,
    Prayer,
    BibleVerse
}
