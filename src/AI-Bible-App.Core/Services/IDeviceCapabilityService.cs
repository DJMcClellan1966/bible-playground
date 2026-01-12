namespace AI_Bible_App.Core.Services;

/// <summary>
/// Detects device capabilities and recommends optimal configuration
/// </summary>
public interface IDeviceCapabilityService
{
    /// <summary>
    /// Detect current device capabilities
    /// </summary>
    Task<DeviceCapabilities> DetectCapabilitiesAsync();
    
    /// <summary>
    /// Get recommended model configuration for this device
    /// </summary>
    Task<ModelConfiguration> GetRecommendedConfigurationAsync();
    
    /// <summary>
    /// Get all available model configurations
    /// </summary>
    List<ModelConfiguration> GetAvailableConfigurations();
    
    /// <summary>
    /// Manually set a specific configuration (user override)
    /// </summary>
    Task SetConfigurationAsync(string tierId);
    
    /// <summary>
    /// Check if device can handle a specific configuration
    /// </summary>
    bool CanHandleConfiguration(ModelConfiguration config, DeviceCapabilities capabilities);
}
