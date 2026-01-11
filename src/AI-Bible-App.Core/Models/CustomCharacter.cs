namespace AI_Bible_App.Core.Models;

/// <summary>
/// User-created custom biblical character that can be saved/loaded from JSON
/// </summary>
public class CustomCharacter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Era { get; set; } = string.Empty;
    public List<string> BiblicalReferences { get; set; } = new();
    
    /// <summary>
    /// Key life experiences to draw from (used to generate system prompt)
    /// </summary>
    public List<string> LifeExperiences { get; set; } = new();
    
    /// <summary>
    /// Character personality traits
    /// </summary>
    public List<string> PersonalityTraits { get; set; } = new();
    
    /// <summary>
    /// Key virtues associated with this character
    /// </summary>
    public List<string> KeyVirtues { get; set; } = new();
    
    /// <summary>
    /// What the character is known for
    /// </summary>
    public string KnownFor { get; set; } = string.Empty;
    
    /// <summary>
    /// Custom system prompt (if provided, overrides auto-generated prompt)
    /// </summary>
    public string? CustomSystemPrompt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Convert to BiblicalCharacter for use in the app
    /// </summary>
    public BiblicalCharacter ToBiblicalCharacter()
    {
        return new BiblicalCharacter
        {
            Id = $"custom_{Id}",
            Name = Name,
            Title = Title,
            Description = Description,
            Era = Era,
            BiblicalReferences = BiblicalReferences,
            SystemPrompt = CustomSystemPrompt ?? GenerateSystemPrompt(),
            Attributes = new Dictionary<string, string>
            {
                { "Personality", string.Join(", ", PersonalityTraits) },
                { "KnownFor", KnownFor },
                { "KeyVirtues", string.Join(", ", KeyVirtues) }
            },
            IconFileName = "custom_character.png",
            Voice = new VoiceConfig
            {
                Pitch = 1.0f,
                Rate = 1.0f,
                Volume = 1.0f,
                Description = $"Custom character - {Name}",
                Locale = "en-US"
            },
            PrimaryTone = EmotionalTone.Compassionate,
            PrayerStyle = PrayerStyle.Spontaneous,
            IsCustom = true
        };
    }
    
    /// <summary>
    /// Generate a system prompt from the character's attributes
    /// </summary>
    private string GenerateSystemPrompt()
    {
        var experiences = string.Join("\n- ", LifeExperiences);
        var personality = string.Join(", ", PersonalityTraits);
        
        return $@"You are {Name} from the Bible, {Description}.

CRITICAL INSTRUCTIONS:
1. LISTEN to what the person says and respond to THEIR specific situation.
2. SHARE your own experiences that RELATE to what they're going through.
3. Be CONCISE - 2-3 paragraphs. Ask follow-up questions.
4. VARY which experiences you draw from - don't repeat the same stories.

Your life experiences to draw from:
- {experiences}

Your characteristics:
- Personality: {personality}
- Known for: {KnownFor}
- Key virtues: {string.Join(", ", KeyVirtues)}

Era: {Era}
Biblical references: {string.Join(", ", BiblicalReferences)}

ALWAYS connect YOUR experience to THEIR situation with a different story each time.";
    }
}

/// <summary>
/// Repository interface for custom characters
/// </summary>
public interface ICustomCharacterRepository
{
    Task<List<CustomCharacter>> GetAllAsync();
    Task<CustomCharacter?> GetByIdAsync(string id);
    Task SaveAsync(CustomCharacter character);
    Task DeleteAsync(string id);
    Task<string> ExportToJsonAsync();
    Task ImportFromJsonAsync(string json);
}
