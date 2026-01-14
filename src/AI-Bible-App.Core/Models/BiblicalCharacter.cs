namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents a biblical character that can interact with users
/// </summary>
public class BiblicalCharacter
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Era { get; set; } = string.Empty;
    public List<string> BiblicalReferences { get; set; } = new();
    public string SystemPrompt { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new();
    
    /// <summary>
    /// Filename of the character's icon (e.g., "david.png")
    /// </summary>
    public string IconFileName { get; set; } = "default_avatar.png";
    
    /// <summary>
    /// Voice configuration for text-to-speech reading of character responses
    /// </summary>
    public VoiceConfig Voice { get; set; } = new();
    
    /// <summary>
    /// Primary emotional tone/personality trait (for emotion system)
    /// </summary>
    public EmotionalTone PrimaryTone { get; set; } = EmotionalTone.Balanced;
    
    /// <summary>
    /// Relationship connections to other characters
    /// </summary>
    public Dictionary<string, string> Relationships { get; set; } = new();
    
    /// <summary>
    /// Prayer style this character uses
    /// </summary>
    public PrayerStyle PrayerStyle { get; set; } = PrayerStyle.Traditional;
    
    /// <summary>
    /// Whether this is a user-created custom character
    /// </summary>
    public bool IsCustom { get; set; } = false;
    
    /// <summary>
    /// Whether this character is enabled for roundtable discussions
    /// </summary>
    public bool RoundtableEnabled { get; set; } = false;

    /// <summary>
    /// Whether this character should act as a contrarian (devil's advocate) when enabled for the session
    /// </summary>
    public bool IsContrarian { get; set; } = false;
}

/// <summary>
/// Configuration for character voice in text-to-speech
/// </summary>
public class VoiceConfig
{
    /// <summary>
    /// Pitch of the voice (0.0 to 2.0, where 1.0 is normal)
    /// Lower values = deeper voice, Higher values = higher voice
    /// </summary>
    public float Pitch { get; set; } = 1.0f;
    
    /// <summary>
    /// Speech rate (0.0 to 2.0, where 1.0 is normal speed)
    /// </summary>
    public float Rate { get; set; } = 1.0f;
    
    /// <summary>
    /// Volume level (0.0 to 1.0)
    /// </summary>
    public float Volume { get; set; } = 1.0f;
    
    /// <summary>
    /// Description of the voice character (for UI display)
    /// e.g., "Kingly and authoritative", "Gentle shepherd", "Bold apostle"
    /// </summary>
    public string Description { get; set; } = "Default voice";
    
    /// <summary>
    /// Preferred locale for the voice (e.g., "en-US", "en-GB")
    /// </summary>
    public string Locale { get; set; } = "en-US";
}

/// <summary>
/// Emotional tones for character responses
/// </summary>
public enum EmotionalTone
{
    Balanced,
    Compassionate,
    Bold,
    Gentle,
    Wise,
    Passionate,
    Humble,
    Authoritative
}

/// <summary>
/// Prayer styles for different characters
/// </summary>
public enum PrayerStyle
{
    Traditional,
    Psalm,
    Structured,
    Spontaneous,
    Contemplative,
    Intercession,
    Conversational,
    Prophetic,
    Confessional
}
