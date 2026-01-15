using System.ComponentModel;

namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents a single message in a chat conversation
/// </summary>
public class ChatMessage : INotifyPropertyChanged
{
    private string _content = string.Empty;
    private List<ContextualReference>? _contextualReferences;
    private bool _isReferencesExpanded;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Role { get; set; } = string.Empty; // "user", "assistant", "system"
    
    public string Content 
    { 
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
            }
        }
    }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string CharacterId { get; set; } = string.Empty;
    
    /// <summary>
    /// Character name (populated for display purposes, not persisted)
    /// </summary>
    public string? CharacterName { get; set; }
    
    /// <summary>
    /// User rating for AI responses: -1 (thumbs down), 0 (no rating), 1 (thumbs up)
    /// Used for future model fine-tuning based on user feedback
    /// </summary>
    public int Rating { get; set; } = 0;
    
    /// <summary>
    /// Optional feedback text from user explaining the rating
    /// </summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// Contextual Bible references showing where the character demonstrated knowledge of the topic
    /// </summary>
    public List<ContextualReference>? ContextualReferences
    {
        get => _contextualReferences;
        set
        {
            if (_contextualReferences != value)
            {
                _contextualReferences = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContextualReferences)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasContextualReferences)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InlineReferencesText)));
            }
        }
    }

    public bool HasContextualReferences => ContextualReferences?.Any() == true;

    public string InlineReferencesText => ContextualReferences?.Any() == true
        ? $"Sources: {string.Join("; ", ContextualReferences.Select(r => r.Reference).Distinct())}"
        : string.Empty;

    public bool IsReferencesExpanded
    {
        get => _isReferencesExpanded;
        set
        {
            if (_isReferencesExpanded != value)
            {
                _isReferencesExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReferencesExpanded)));
            }
        }
    }
}

/// <summary>
/// Represents a contextual Bible reference for a character's response
/// </summary>
public class ContextualReference
{
    public string Reference { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Connection { get; set; } = string.Empty;
}
