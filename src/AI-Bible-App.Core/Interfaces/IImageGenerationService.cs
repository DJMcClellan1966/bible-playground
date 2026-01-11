using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Service for generating images using AI (DALL-E, Stable Diffusion, etc.)
/// </summary>
public interface IImageGenerationService
{
    /// <summary>
    /// Generate a character portrait
    /// </summary>
    Task<ImageGenerationResult> GenerateCharacterPortraitAsync(
        BiblicalCharacter character,
        ImageStyle style = ImageStyle.OilPainting,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a scene illustration
    /// </summary>
    Task<ImageGenerationResult> GenerateSceneAsync(
        string sceneDescription,
        ImageStyle style = ImageStyle.OilPainting,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a roundtable discussion scene
    /// </summary>
    Task<ImageGenerationResult> GenerateRoundtableSceneAsync(
        List<BiblicalCharacter> participants,
        string topic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the service is available and configured
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Get cached portrait for a character if available
    /// </summary>
    Task<string?> GetCachedPortraitAsync(string characterId);
}

/// <summary>
/// Result of an image generation request
/// </summary>
public class ImageGenerationResult
{
    public bool Success { get; set; }
    public string? ImagePath { get; set; }
    public string? ImageUrl { get; set; }
    public byte[]? ImageData { get; set; }
    public string? Base64Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Prompt { get; set; }
}

/// <summary>
/// Visual styles for generated images
/// </summary>
public enum ImageStyle
{
    OilPainting,        // Classical oil painting style
    Watercolor,         // Soft watercolor
    Renaissance,        // Renaissance master style
    Byzantine,          // Byzantine icon style
    StainedGlass,       // Cathedral stained glass
    Illuminated,        // Medieval illuminated manuscript
    Modern,             // Modern realistic
    Anime,              // Anime/manga style
    Sketch              // Pencil sketch
}
