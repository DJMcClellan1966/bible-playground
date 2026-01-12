namespace AI_Bible_App.Core.Models;

/// <summary>
/// Device hardware capabilities detected at runtime
/// </summary>
public class DeviceCapabilities
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // Windows, Android, iOS, macOS
    
    // Memory
    public long TotalMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public double MemoryUsagePercent => TotalMemoryBytes > 0 
        ? (double)(TotalMemoryBytes - AvailableMemoryBytes) / TotalMemoryBytes * 100 
        : 0;
    
    // Storage
    public long TotalStorageBytes { get; set; }
    public long AvailableStorageBytes { get; set; }
    
    // GPU
    public bool HasDedicatedGpu { get; set; }
    public string? GpuName { get; set; }
    public long? GpuMemoryBytes { get; set; }
    
    // CPU
    public int CpuCoreCount { get; set; }
    public string? CpuArchitecture { get; set; }
    
    // Performance tier (calculated)
    public DevicePerformanceTier PerformanceTier { get; set; }
    
    // Recommended configuration
    public string RecommendedModelSize { get; set; } = "auto";
    public int RecommendedMaxContextLength { get; set; }
    public bool RecommendOffloading { get; set; }
    
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

public enum DevicePerformanceTier
{
    Low = 1,      // < 4GB RAM, no GPU, older CPU (e.g., budget phones, old PCs)
    Medium = 2,   // 4-8GB RAM, integrated GPU, modern CPU (e.g., mid-range devices)
    High = 3,     // 8-16GB RAM, decent GPU, fast CPU (e.g., gaming laptops, modern desktops)
    Ultra = 4     // > 16GB RAM, dedicated GPU, high-end CPU (e.g., workstations, gaming rigs)
}

/// <summary>
/// Model configuration tier based on device capabilities
/// </summary>
public class ModelConfiguration
{
    public string TierId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DevicePerformanceTier MinimumTier { get; set; }
    
    // Model settings
    public string ModelName { get; set; } = string.Empty;
    public string ModelSize { get; set; } = string.Empty; // "mini", "small", "medium", "large"
    public long EstimatedMemoryMb { get; set; }
    public int ContextLength { get; set; }
    public int MaxPredictTokens { get; set; }
    
    // Knowledge base settings
    public int MaxHistoricalContexts { get; set; }
    public int MaxLanguageInsights { get; set; }
    public int MaxThematicConnections { get; set; }
    public bool UseKnowledgeBasePagination { get; set; }
    
    // Feature flags
    public bool EnableVoiceChat { get; set; }
    public bool EnableMultiCharacterChat { get; set; }
    public bool EnableAdvancedRAG { get; set; }
    public bool PreferCloudOffloading { get; set; }
    
    // Performance settings
    public int NumGpuLayers { get; set; } // How many layers to offload to GPU
    public int NumThreads { get; set; }
    public int BatchSize { get; set; }
    
    public string Description { get; set; } = string.Empty;
}
