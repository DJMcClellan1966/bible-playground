using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Cloud sync service using Azure Blob Storage (or local file simulation for development).
/// Enables users to sync their data across devices using a simple sync code.
/// </summary>
public class CloudSyncService : ICloudSyncService
{
    private readonly ILogger<CloudSyncService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IPrayerRepository _prayerRepository;
    private readonly IReflectionRepository _reflectionRepository;
    private readonly ICharacterMemoryService _memoryService;
    private readonly string _syncStoragePath;
    
    // Words for generating memorable sync codes
    private static readonly string[] CodeWords = new[]
    {
        "FAITH", "HOPE", "LOVE", "GRACE", "PEACE", "TRUTH", "LIGHT", "WORD",
        "PRAY", "BLESS", "TRUST", "GLORY", "MERCY", "SPIRIT", "HEART", "SOUL",
        "LAMB", "DOVE", "LION", "ROCK", "VINE", "BREAD", "PATH", "LIFE"
    };
    
    public bool IsSyncEnabled { get; private set; }
    public string? CurrentSyncCode { get; private set; }

    public CloudSyncService(
        ILogger<CloudSyncService> logger,
        IUserRepository userRepository,
        IChatRepository chatRepository,
        IPrayerRepository prayerRepository,
        IReflectionRepository reflectionRepository,
        ICharacterMemoryService memoryService)
    {
        _logger = logger;
        _userRepository = userRepository;
        _chatRepository = chatRepository;
        _prayerRepository = prayerRepository;
        _reflectionRepository = reflectionRepository;
        _memoryService = memoryService;
        
        // For now, use a local "cloud" folder - can be replaced with Azure/Firebase later
        _syncStoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AI-Bible-App",
            "cloud-sync");
        
        Directory.CreateDirectory(_syncStoragePath);
    }

    public async Task<SyncCodeResult> GenerateSyncCodeAsync(string userId)
    {
        try
        {
            var user = await _userRepository.GetUserAsync(userId);
            if (user == null)
            {
                return new SyncCodeResult { Success = false, ErrorMessage = "User not found" };
            }
            
            // Generate a memorable sync code
            var syncCode = GenerateMemorableSyncCode();
            var cloudUserId = Guid.NewGuid().ToString();
            
            // Set up sync identity
            user.SyncIdentity = new SyncIdentity
            {
                SyncCode = syncCode,
                CloudUserId = cloudUserId,
                SyncEnabledAt = DateTime.UtcNow,
                DeviceId = GetDeviceId(),
                DeviceName = GetDeviceName()
            };
            
            await _userRepository.SaveUserAsync(user);
            
            // Gather all user data
            var syncData = await GatherUserDataAsync(user);
            
            // Save to "cloud" (local simulation for now)
            await SaveToCloudAsync(syncCode, syncData);
            
            CurrentSyncCode = syncCode;
            IsSyncEnabled = true;
            
            _logger.LogInformation("Generated sync code {SyncCode} for user {UserId}", syncCode, userId);
            
            return new SyncCodeResult
            {
                Success = true,
                SyncCode = syncCode,
                ExpiresAt = null // Codes don't expire
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate sync code for user {UserId}", userId);
            return new SyncCodeResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncLinkResult> LinkWithSyncCodeAsync(string syncCode)
    {
        try
        {
            // Normalize the code
            syncCode = syncCode.ToUpperInvariant().Trim();
            
            // Try to load data from cloud
            var syncData = await LoadFromCloudAsync(syncCode);
            if (syncData == null)
            {
                return new SyncLinkResult 
                { 
                    Success = false, 
                    ErrorMessage = "Invalid sync code. Please check and try again." 
                };
            }
            
            // Create or update local user with synced data
            var existingUsers = await _userRepository.GetAllUsersAsync();
            var existingUser = existingUsers.FirstOrDefault(u => 
                u.SyncIdentity?.CloudUserId == syncData.CloudUserId);
            
            AppUser user;
            if (existingUser != null)
            {
                user = existingUser;
            }
            else
            {
                // Create new local user from sync data
                user = new AppUser
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = syncData.Profile.Name,
                    AvatarEmoji = syncData.Profile.AvatarEmoji,
                    CreatedAt = syncData.Profile.CreatedAt,
                    Settings = syncData.Profile.Settings
                };
            }
            
            // Set sync identity for this device
            user.SyncIdentity = new SyncIdentity
            {
                SyncCode = syncCode,
                CloudUserId = syncData.CloudUserId,
                SyncEnabledAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow,
                DeviceId = GetDeviceId(),
                DeviceName = GetDeviceName()
            };
            
            await _userRepository.SaveUserAsync(user);
            
            // Import the synced data
            var itemsSynced = await ImportSyncDataAsync(user.Id, syncData);
            
            CurrentSyncCode = syncCode;
            IsSyncEnabled = true;
            
            _logger.LogInformation("Linked device with sync code {SyncCode}, imported {Items} items", 
                syncCode, itemsSynced);
            
            return new SyncLinkResult
            {
                Success = true,
                LinkedUserId = user.Id,
                UserName = user.Name,
                ItemsSynced = itemsSynced
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to link with sync code {SyncCode}", syncCode);
            return new SyncLinkResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncResult> SyncToCloudAsync(string userId)
    {
        try
        {
            var user = await _userRepository.GetUserAsync(userId);
            if (user?.SyncIdentity == null)
            {
                return new SyncResult { Success = false, ErrorMessage = "Sync not enabled" };
            }
            
            var syncData = await GatherUserDataAsync(user);
            await SaveToCloudAsync(user.SyncIdentity.SyncCode, syncData);
            
            user.SyncIdentity.LastSyncedAt = DateTime.UtcNow;
            await _userRepository.SaveUserAsync(user);
            
            return new SyncResult
            {
                Success = true,
                ItemsUploaded = CountSyncItems(syncData),
                SyncedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync to cloud for user {UserId}", userId);
            return new SyncResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncResult> SyncFromCloudAsync(string userId)
    {
        try
        {
            var user = await _userRepository.GetUserAsync(userId);
            if (user?.SyncIdentity == null)
            {
                return new SyncResult { Success = false, ErrorMessage = "Sync not enabled" };
            }
            
            var syncData = await LoadFromCloudAsync(user.SyncIdentity.SyncCode);
            if (syncData == null)
            {
                return new SyncResult { Success = false, ErrorMessage = "No cloud data found" };
            }
            
            var itemsDownloaded = await ImportSyncDataAsync(userId, syncData);
            
            user.SyncIdentity.LastSyncedAt = DateTime.UtcNow;
            await _userRepository.SaveUserAsync(user);
            
            return new SyncResult
            {
                Success = true,
                ItemsDownloaded = itemsDownloaded,
                SyncedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync from cloud for user {UserId}", userId);
            return new SyncResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncResult> FullSyncAsync(string userId)
    {
        // For simplicity, upload local then download cloud (cloud wins on conflicts)
        var uploadResult = await SyncToCloudAsync(userId);
        if (!uploadResult.Success)
            return uploadResult;
        
        var downloadResult = await SyncFromCloudAsync(userId);
        
        return new SyncResult
        {
            Success = downloadResult.Success,
            ItemsUploaded = uploadResult.ItemsUploaded,
            ItemsDownloaded = downloadResult.ItemsDownloaded,
            ErrorMessage = downloadResult.ErrorMessage,
            SyncedAt = DateTime.UtcNow
        };
    }

    public async Task DisableSyncAsync(string userId, bool deleteCloudData = false)
    {
        var user = await _userRepository.GetUserAsync(userId);
        if (user?.SyncIdentity == null) return;
        
        if (deleteCloudData)
        {
            var cloudPath = GetCloudFilePath(user.SyncIdentity.SyncCode);
            if (File.Exists(cloudPath))
            {
                File.Delete(cloudPath);
            }
        }
        
        user.SyncIdentity = null;
        await _userRepository.SaveUserAsync(user);
        
        CurrentSyncCode = null;
        IsSyncEnabled = false;
        
        _logger.LogInformation("Disabled sync for user {UserId}", userId);
    }

    public async Task<SyncStatus> GetSyncStatusAsync(string userId)
    {
        var user = await _userRepository.GetUserAsync(userId);
        
        if (user?.SyncIdentity == null)
        {
            return new SyncStatus { IsEnabled = false };
        }
        
        var cloudPath = GetCloudFilePath(user.SyncIdentity.SyncCode);
        var cloudExists = File.Exists(cloudPath);
        
        return new SyncStatus
        {
            IsEnabled = true,
            SyncCode = user.SyncIdentity.SyncCode,
            LastSyncedAt = user.SyncIdentity.LastSyncedAt,
            LinkedDevices = 1, // Would need cloud metadata to track this properly
            CloudProvider = "Local Storage", // Would be "Azure" or "Firebase" in production
            Health = cloudExists ? SyncHealth.Healthy : SyncHealth.NeedsSync
        };
    }

    #region Private Helper Methods

    private string GenerateMemorableSyncCode()
    {
        // Generate code like "FAITH-7X3K-HOPE"
        var random = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        random.GetBytes(bytes);
        
        var word1 = CodeWords[bytes[0] % CodeWords.Length];
        var word2 = CodeWords[bytes[1] % CodeWords.Length];
        var alphaNum = Convert.ToBase64String(bytes).Substring(0, 4).ToUpperInvariant()
            .Replace("+", "X").Replace("/", "K").Replace("=", "");
        
        return $"{word1}-{alphaNum}-{word2}";
    }

    private string GetDeviceId()
    {
        // Try to get a stable device identifier
        var deviceIdPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AI-Bible-App",
            ".device-id");
        
        if (File.Exists(deviceIdPath))
        {
            return File.ReadAllText(deviceIdPath).Trim();
        }
        
        var deviceId = Guid.NewGuid().ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(deviceIdPath)!);
        File.WriteAllText(deviceIdPath, deviceId);
        return deviceId;
    }

    private string GetDeviceName()
    {
        return Environment.MachineName;
    }

    private string GetCloudFilePath(string syncCode)
    {
        // Hash the sync code to get a filename (don't expose sync codes in filenames)
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(syncCode))).Substring(0, 16);
        return Path.Combine(_syncStoragePath, $"sync_{hash}.json");
    }

    private async Task<UserSyncData> GatherUserDataAsync(AppUser user)
    {
        var syncData = new UserSyncData
        {
            CloudUserId = user.SyncIdentity?.CloudUserId ?? Guid.NewGuid().ToString(),
            Profile = new SyncedUserProfile
            {
                Name = user.Name,
                AvatarEmoji = user.AvatarEmoji,
                CreatedAt = user.CreatedAt,
                Settings = user.Settings
            },
            LastModified = DateTime.UtcNow,
            LastModifiedByDevice = GetDeviceId()
        };
        
        // Gather character memories
        syncData.CharacterMemories = await _memoryService.GetAllMemoriesForUserAsync(user.Id);
        
        // Gather chat sessions
        syncData.ChatSessions = await _chatRepository.GetAllSessionsForUserAsync(user.Id);
        
        // Gather prayers
        syncData.Prayers = await _prayerRepository.GetAllForUserAsync(user.Id);
        
        // Gather reflections
        syncData.Reflections = await _reflectionRepository.GetAllForUserAsync(user.Id);
        
        return syncData;
    }

    private async Task SaveToCloudAsync(string syncCode, UserSyncData syncData)
    {
        var path = GetCloudFilePath(syncCode);
        var json = JsonSerializer.Serialize(syncData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        
        _logger.LogDebug("Saved sync data to {Path}", path);
    }

    private async Task<UserSyncData?> LoadFromCloudAsync(string syncCode)
    {
        var path = GetCloudFilePath(syncCode);
        
        if (!File.Exists(path))
        {
            return null;
        }
        
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<UserSyncData>(json);
    }

    private async Task<int> ImportSyncDataAsync(string localUserId, UserSyncData syncData)
    {
        var itemCount = 0;
        
        // Import character memories
        foreach (var memory in syncData.CharacterMemories)
        {
            memory.UserId = localUserId; // Update to local user ID
            await _memoryService.SaveMemoryAsync(memory);
            itemCount++;
        }
        
        // Import chat sessions
        foreach (var session in syncData.ChatSessions)
        {
            session.UserId = localUserId;
            await _chatRepository.SaveSessionAsync(session);
            itemCount++;
        }
        
        // Import prayers
        foreach (var prayer in syncData.Prayers)
        {
            prayer.UserId = localUserId;
            await _prayerRepository.SaveAsync(prayer);
            itemCount++;
        }
        
        // Import reflections
        foreach (var reflection in syncData.Reflections)
        {
            reflection.UserId = localUserId;
            await _reflectionRepository.SaveAsync(reflection);
            itemCount++;
        }
        
        return itemCount;
    }

    private int CountSyncItems(UserSyncData syncData)
    {
        return syncData.CharacterMemories.Count +
               syncData.ChatSessions.Count +
               syncData.Prayers.Count +
               syncData.Reflections.Count;
    }

    #endregion
}
