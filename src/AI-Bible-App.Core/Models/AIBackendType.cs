namespace AI_Bible_App.Core.Models;

/// <summary>
/// Device capability tiers for AI backend selection (for test compatibility)
/// </summary>
public enum DeviceCapabilityTier
{
    Minimal,
    Low,
    Medium,
    High
}

/// <summary>
/// Type of AI backend
/// </summary>
public enum AIBackendType
{
    LocalOllama,
    OnDevice,
    Cloud,
    Cached,
    AzureOpenAI
}

/// <summary>
/// Recommended AI backend based on device capabilities
/// </summary>
public class AIBackendRecommendation
{
    public AIBackendType PrimaryBackend { get; set; }
    // For test compatibility
    public AIBackendType Primary { get; set; }
    public AIBackendType FallbackBackend { get; set; }
    public string Reason { get; set; } = string.Empty;

    // For test compatibility
    public AIBackendType Fallback { get; set; }
    public AIBackendType Emergency { get; set; }
    public string RecommendedModelName { get; set; } = string.Empty;
    public int RecommendedContextSize { get; set; }
}
