using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Service for syncing user data across devices using sync codes.
/// Allows users to link their identity across phone, tablet, and desktop.
/// </summary>
public interface ICloudSyncService
{
    /// <summary>
    /// Whether cloud sync is currently enabled for this user
    /// </summary>
    bool IsSyncEnabled { get; }
    
    /// <summary>
    /// The current user's sync code (if they have one)
    /// </summary>
    string? CurrentSyncCode { get; }
    
    /// <summary>
    /// Generate a new sync code for the current user.
    /// This uploads their data to the cloud and returns a shareable code.
    /// </summary>
    Task<SyncCodeResult> GenerateSyncCodeAsync(string userId);
    
    /// <summary>
    /// Link this device to an existing sync code from another device.
    /// Downloads the user's data from the cloud.
    /// </summary>
    Task<SyncLinkResult> LinkWithSyncCodeAsync(string syncCode);
    
    /// <summary>
    /// Sync local changes to the cloud
    /// </summary>
    Task<SyncResult> SyncToCloudAsync(string userId);
    
    /// <summary>
    /// Pull latest changes from the cloud
    /// </summary>
    Task<SyncResult> SyncFromCloudAsync(string userId);
    
    /// <summary>
    /// Perform a full two-way sync
    /// </summary>
    Task<SyncResult> FullSyncAsync(string userId);
    
    /// <summary>
    /// Disable sync and remove cloud data (optional)
    /// </summary>
    Task DisableSyncAsync(string userId, bool deleteCloudData = false);
    
    /// <summary>
    /// Get sync status information
    /// </summary>
    Task<SyncStatus> GetSyncStatusAsync(string userId);
}

/// <summary>
/// Result of generating a sync code
/// </summary>
public class SyncCodeResult
{
    public bool Success { get; set; }
    public string? SyncCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Result of linking with a sync code
/// </summary>
public class SyncLinkResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LinkedUserId { get; set; }
    public string? UserName { get; set; }
    public int ItemsSynced { get; set; }
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsUploaded { get; set; }
    public int ItemsDownloaded { get; set; }
    public int Conflicts { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Current sync status
/// </summary>
public class SyncStatus
{
    public bool IsEnabled { get; set; }
    public string? SyncCode { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public int LinkedDevices { get; set; }
    public string? CloudProvider { get; set; }
    public SyncHealth Health { get; set; } = SyncHealth.Unknown;
}

public enum SyncHealth
{
    Unknown,
    Healthy,
    NeedsSync,
    SyncFailed,
    Offline
}
