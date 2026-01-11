namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents a generated prayer
/// </summary>
public class Prayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Prayer saved by a user with additional metadata
/// </summary>
public class SavedPrayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastPrayedAt { get; set; }
    public bool IsFavorite { get; set; }
    public List<string> Tags { get; set; } = new();
}
