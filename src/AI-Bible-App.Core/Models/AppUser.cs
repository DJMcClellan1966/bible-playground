namespace AI_Bible_App.Core.Models;

/// <summary>
/// Represents an application user with their profile and settings
/// </summary>
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? AvatarEmoji { get; set; } = "👤";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public UserSettings Settings { get; set; } = new();
    
    /// <summary>
    /// Optional PIN hash for privacy protection (null = no PIN required)
    /// </summary>
    public string? PinHash { get; set; }
    
    /// <summary>
    /// Whether this user has PIN protection enabled
    /// </summary>
    public bool HasPin => !string.IsNullOrEmpty(PinHash);
    
    /// <summary>
    /// Cloud sync identity - shared across all devices for this user
    /// </summary>
    public SyncIdentity? SyncIdentity { get; set; }
    
    /// <summary>
    /// Whether this user has cloud sync enabled
    /// </summary>
    public bool HasSyncEnabled => SyncIdentity != null && !string.IsNullOrEmpty(SyncIdentity.SyncCode);
    
    /// <summary>
    /// User's subscription information (tier, status, billing)
    /// </summary>
    public UserSubscription? Subscription { get; set; }
    
    /// <summary>
    /// Email address for subscription/billing (optional for free tier)
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Authentication provider used to sign in (Email, Google, Apple)
    /// </summary>
    public string AuthProvider { get; set; } = "Anonymous";
    
    /// <summary>
    /// External provider user ID (for Google/Apple sign-in)
    /// </summary>
    public string? ExternalAuthId { get; set; }
    
    /// <summary>
    /// Hashed password (for email authentication only)
    /// </summary>
    public string? PasswordHash { get; set; }
    
    /// <summary>
    /// Profile photo URL (from Google/Apple or uploaded)
    /// </summary>
    public string? ProfilePhotoUrl { get; set; }
}

/// <summary>
/// Identity information for cross-device sync
/// </summary>
public class SyncIdentity
{
    /// <summary>
    /// The sync code used to link devices (e.g., "FAITH-7X3K-HOPE")
    /// </summary>
    public string SyncCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Cloud-side unique identifier (stays constant across devices)
    /// </summary>
    public string CloudUserId { get; set; } = string.Empty;
    
    /// <summary>
    /// When sync was first enabled
    /// </summary>
    public DateTime SyncEnabledAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last successful sync time
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
    
    /// <summary>
    /// Unique identifier for THIS device (for conflict resolution)
    /// </summary>
    public string DeviceId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Friendly name for this device
    /// </summary>
    public string? DeviceName { get; set; }
}

/// <summary>
/// User-specific settings and preferences
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Whether content moderation is enabled (default: true)
    /// </summary>
    public bool EnableContentModeration { get; set; } = true;
    
    /// <summary>
    /// Preferred AI backend (local/cloud/auto)
    /// </summary>
    public string PreferredAIBackend { get; set; } = "auto";
    
    /// <summary>
    /// User's preferred Bible version (kjv, web, asv)
    /// </summary>
    public string PreferredBibleVersion { get; set; } = "kjv";
    
    /// <summary>
    /// Theme preference: "System", "Light", or "Dark"
    /// </summary>
    public string ThemePreference { get; set; } = "System";
    
    /// <summary>
    /// Font size multiplier: "Small" (0.85), "Medium" (1.0), "Large" (1.2), "ExtraLarge" (1.4)
    /// </summary>
    public string FontSizePreference { get; set; } = "Medium";
    
    /// <summary>
    /// Whether user consents to share anonymized questions for model improvement (default: false)
    /// </summary>
    public bool ShareDataForImprovement { get; set; } = false;
}
