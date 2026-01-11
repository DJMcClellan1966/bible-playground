using System.Net.Http.Json;
using System.Text.Json;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Image generation service supporting DALL-E, local Stable Diffusion, and fallback placeholders
/// </summary>
public class ImageGenerationService : IImageGenerationService
{
    private readonly ILogger<ImageGenerationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _openAiApiKey;
    private readonly string? _stableDiffusionUrl;
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, string> _characterColorMap;

    public ImageGenerationService(
        ILogger<ImageGenerationService> logger,
        IConfiguration configuration,
        HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        _openAiApiKey = configuration["OpenAI:ApiKey"];
        _stableDiffusionUrl = configuration["StableDiffusion:Url"];
        
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App", "ImageCache");
        
        Directory.CreateDirectory(_cacheDirectory);
        
        // Character-specific color themes for visual consistency
        _characterColorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Old Testament
            {"moses", "#8B4513,#D4AF37"},      // Brown/Gold - desert, tablets
            {"david", "#4169E1,#FFD700"},      // Royal blue/Gold - king
            {"abraham", "#8B7355,#F5DEB3"},    // Earth tones - patriarch
            {"solomon", "#800080,#FFD700"},    // Purple/Gold - wisdom, wealth
            {"elijah", "#DC143C,#FF6347"},     // Fiery red - chariot of fire
            {"isaiah", "#4B0082,#E6E6FA"},     // Indigo/Lavender - prophecy
            {"daniel", "#000080,#C0C0C0"},     // Navy/Silver - Babylon
            {"joseph", "#FFD700,#800080"},     // Gold/Purple - coat, Egypt
            {"ruth", "#DDA0DD,#F5DEB3"},       // Plum/Wheat - harvest
            {"esther", "#FF69B4,#FFD700"},     // Pink/Gold - queen
            {"job", "#696969,#F0E68C"},        // Gray/Khaki - suffering/restoration
            {"noah", "#4682B4,#8B4513"},       // Steel blue/Brown - flood, ark
            {"jeremiah", "#2F4F4F,#708090"},   // Dark slate - weeping prophet
            {"ezekiel", "#9932CC,#00CED1"},    // Purple/Cyan - visions
            
            // New Testament
            {"jesus", "#FFFFFF,#FFD700"},      // White/Gold - divine
            {"peter", "#1E90FF,#A9A9A9"},      // Blue/Gray - fisherman, rock
            {"paul", "#8B0000,#D2691E"},       // Dark red/Tan - tent maker, martyr
            {"john", "#FF4500,#FFFAF0"},       // Red-orange/Floral white - beloved disciple
            {"mary", "#87CEEB,#FFFFFF"},       // Sky blue/White - mother of Jesus
            {"martha", "#D2B48C,#F5F5DC"},     // Tan/Beige - hospitality
            {"marymagdalene", "#9370DB,#FFB6C1"}, // Purple/Pink - devotion
            {"thomas", "#A52A2A,#F4A460"},     // Brown/Sandy - doubt to faith
            {"james", "#228B22,#F0FFF0"},      // Forest green - brother of John
            {"luke", "#20B2AA,#FAFAD2"},       // Teal/Light yellow - physician
            {"mark", "#B22222,#FFDAB9"},       // Firebrick/Peach - young helper
            {"matthew", "#DAA520,#FFFACD"},    // Goldenrod/Lemon - tax collector
            {"barnabas", "#32CD32,#FFFAF0"},   // Lime/Floral white - encourager
            {"timothy", "#6B8E23,#F5F5DC"},    // Olive/Beige - young pastor
            {"stephen", "#FF6347,#FFFAFA"},    // Tomato/Snow - first martyr
            {"priscilla", "#DB7093,#FFF0F5"},  // Pale violet/Lavender - teacher
        };
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_openAiApiKey) || !string.IsNullOrEmpty(_stableDiffusionUrl);

    public async Task<ImageGenerationResult> GenerateCharacterPortraitAsync(
        BiblicalCharacter character,
        ImageStyle style = ImageStyle.OilPainting,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cachedPath = await GetCachedPortraitAsync(character.Id);
        if (!string.IsNullOrEmpty(cachedPath) && File.Exists(cachedPath))
        {
            return new ImageGenerationResult
            {
                Success = true,
                ImagePath = cachedPath
            };
        }

        var prompt = BuildCharacterPrompt(character, style);
        
        try
        {
            // Try DALL-E first
            if (!string.IsNullOrEmpty(_openAiApiKey))
            {
                return await GenerateWithDallEAsync(prompt, character.Id, cancellationToken);
            }
            
            // Try local Stable Diffusion
            if (!string.IsNullOrEmpty(_stableDiffusionUrl))
            {
                return await GenerateWithStableDiffusionAsync(prompt, character.Id, cancellationToken);
            }
            
            // Generate a beautiful placeholder
            return GeneratePlaceholderPortrait(character);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate portrait for {CharacterName}", character.Name);
            return GeneratePlaceholderPortrait(character);
        }
    }

    public async Task<ImageGenerationResult> GenerateSceneAsync(
        string sceneDescription,
        ImageStyle style = ImageStyle.OilPainting,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"{GetStylePrefix(style)} Biblical scene: {sceneDescription}. " +
                     "Historically accurate Middle Eastern setting, dramatic lighting, reverent atmosphere.";
        
        try
        {
            if (!string.IsNullOrEmpty(_openAiApiKey))
            {
                var sceneId = $"scene_{sceneDescription.GetHashCode():X8}";
                return await GenerateWithDallEAsync(prompt, sceneId, cancellationToken);
            }
            
            return new ImageGenerationResult
            {
                Success = false,
                ErrorMessage = "No image generation service configured"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate scene");
            return new ImageGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ImageGenerationResult> GenerateRoundtableSceneAsync(
        List<BiblicalCharacter> participants,
        string topic,
        CancellationToken cancellationToken = default)
    {
        var characterNames = string.Join(", ", participants.Select(p => p.Name));
        var prompt = $"Renaissance-style oil painting of a scholarly discussion. " +
                     $"{characterNames} seated around an ancient wooden table, " +
                     $"discussing '{topic}'. Warm candlelight, ancient scrolls, " +
                     "dramatic chiaroscuro lighting, detailed robes and period clothing, " +
                     "expressive faces showing deep contemplation and engagement.";
        
        try
        {
            if (!string.IsNullOrEmpty(_openAiApiKey))
            {
                var sceneId = $"roundtable_{string.Join("_", participants.Select(p => p.Id))}";
                return await GenerateWithDallEAsync(prompt, sceneId, cancellationToken);
            }
            
            return GenerateRoundtablePlaceholder(participants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate roundtable scene");
            return GenerateRoundtablePlaceholder(participants);
        }
    }

    public Task<string?> GetCachedPortraitAsync(string characterId)
    {
        var path = Path.Combine(_cacheDirectory, $"{characterId}_portrait.png");
        return Task.FromResult(File.Exists(path) ? path : null);
    }

    private string BuildCharacterPrompt(BiblicalCharacter character, ImageStyle style)
    {
        var stylePrefix = GetStylePrefix(style);
        var era = character.Era ?? "Biblical times";
        var description = character.Description ?? "";
        
        // Extract key visual characteristics
        var visualHints = GetCharacterVisualHints(character.Name);
        
        return $"{stylePrefix} Portrait of {character.Name}, {character.Title}. " +
               $"{visualHints} " +
               $"Setting: {era}. {description} " +
               "Dignified expression, historically accurate Middle Eastern features, " +
               "period-appropriate clothing, soft dramatic lighting, museum quality.";
    }

    private string GetStylePrefix(ImageStyle style) => style switch
    {
        ImageStyle.OilPainting => "Classical oil painting in the style of Rembrandt,",
        ImageStyle.Watercolor => "Delicate watercolor painting,",
        ImageStyle.Renaissance => "Italian Renaissance masterpiece style,",
        ImageStyle.Byzantine => "Byzantine icon with gold leaf background,",
        ImageStyle.Illuminated => "Medieval illuminated manuscript illustration,",
        ImageStyle.Modern => "Photorealistic digital art,",
        ImageStyle.Anime => "Studio Ghibli inspired anime style,",
        ImageStyle.Sketch => "Detailed pencil sketch,",
        _ => "Classical oil painting,"
    };

    private string GetCharacterVisualHints(string name)
    {
        var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"moses", "elderly man with long white beard, weathered face, holding stone tablets, shepherd's staff"},
            {"david", "young man with reddish hair, crown, harp, shepherd's clothing or royal robes"},
            {"abraham", "ancient patriarch with long gray beard, nomadic robes, wise eyes"},
            {"solomon", "regal king with ornate crown, luxurious purple robes, wise contemplative expression"},
            {"elijah", "intense prophet with wild hair and beard, rough clothing, fiery eyes"},
            {"peter", "sturdy fisherman with curly gray hair and beard, weathered hands, earnest expression"},
            {"paul", "balding man with dark beard, intense scholarly gaze, simple traveling robes"},
            {"john", "young man with gentle features, thoughtful expression, close to Jesus"},
            {"mary", "young woman with modest head covering, serene peaceful expression, blue robes"},
            {"jesus", "compassionate face, long brown hair and beard, white and red robes, divine light"},
            {"daniel", "noble young man in Babylonian court attire, wise beyond his years"},
            {"ruth", "young woman with modest head covering, determined loyal expression, harvest setting"},
            {"esther", "beautiful queen with ornate Persian royal attire, courageous expression"},
        };
        
        return hints.TryGetValue(name, out var hint) ? hint : "dignified biblical figure";
    }

    private async Task<ImageGenerationResult> GenerateWithDallEAsync(
        string prompt, string cacheId, CancellationToken cancellationToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiApiKey);
        
        var request = new
        {
            model = "dall-e-3",
            prompt = prompt,
            n = 1,
            size = "1024x1024",
            quality = "standard"
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/images/generations",
            request,
            cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<DallEResponse>(cancellationToken: cancellationToken);
            if (result?.Data?.FirstOrDefault()?.Url is string imageUrl)
            {
                // Download and cache the image
                var imageData = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
                var cachePath = Path.Combine(_cacheDirectory, $"{cacheId}_portrait.png");
                await File.WriteAllBytesAsync(cachePath, imageData, cancellationToken);
                
                return new ImageGenerationResult
                {
                    Success = true,
                    ImagePath = cachePath,
                    ImageUrl = imageUrl,
                    Prompt = prompt
                };
            }
        }
        
        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("DALL-E API error: {Error}", error);
        
        return new ImageGenerationResult
        {
            Success = false,
            ErrorMessage = $"DALL-E API error: {response.StatusCode}"
        };
    }

    private async Task<ImageGenerationResult> GenerateWithStableDiffusionAsync(
        string prompt, string cacheId, CancellationToken cancellationToken)
    {
        // Automatic1111 API format
        var request = new
        {
            prompt = prompt,
            negative_prompt = "modern clothing, contemporary items, cartoon, low quality, blurry",
            steps = 30,
            cfg_scale = 7.5,
            width = 512,
            height = 512,
            sampler_name = "Euler a"
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            $"{_stableDiffusionUrl}/sdapi/v1/txt2img",
            request,
            cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<StableDiffusionResponse>(cancellationToken: cancellationToken);
            if (result?.Images?.FirstOrDefault() is string base64Image)
            {
                var imageData = Convert.FromBase64String(base64Image);
                var cachePath = Path.Combine(_cacheDirectory, $"{cacheId}_portrait.png");
                await File.WriteAllBytesAsync(cachePath, imageData, cancellationToken);
                
                return new ImageGenerationResult
                {
                    Success = true,
                    ImagePath = cachePath,
                    Base64Data = base64Image,
                    Prompt = prompt
                };
            }
        }
        
        return new ImageGenerationResult
        {
            Success = false,
            ErrorMessage = "Stable Diffusion generation failed"
        };
    }

    private ImageGenerationResult GeneratePlaceholderPortrait(BiblicalCharacter character)
    {
        // Return gradient colors for the character
        var colors = GetCharacterColors(character.Name);
        var initials = GetInitials(character.Name);
        
        return new ImageGenerationResult
        {
            Success = true,
            ImagePath = null, // Will use gradient in UI
            Prompt = $"PLACEHOLDER:{colors.primary}|{colors.secondary}|{initials}"
        };
    }

    private ImageGenerationResult GenerateRoundtablePlaceholder(List<BiblicalCharacter> participants)
    {
        var colors = participants.Select(p => GetCharacterColors(p.Name).primary);
        return new ImageGenerationResult
        {
            Success = true,
            ImagePath = null,
            Prompt = $"ROUNDTABLE:{string.Join(",", colors)}"
        };
    }

    public (string primary, string secondary) GetCharacterColors(string characterName)
    {
        var normalizedName = characterName.ToLower().Replace(" ", "");
        
        if (_characterColorMap.TryGetValue(normalizedName, out var colors))
        {
            var parts = colors.Split(',');
            return (parts[0], parts.Length > 1 ? parts[1] : parts[0]);
        }
        
        // Generate consistent colors based on name hash
        var hash = characterName.GetHashCode();
        var hue = Math.Abs(hash % 360);
        var primary = HslToHex(hue, 0.6, 0.4);
        var secondary = HslToHex((hue + 30) % 360, 0.5, 0.6);
        
        return (primary, secondary);
    }

    private string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        return name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
    }

    private string HslToHex(double h, double s, double l)
    {
        double r, g, b;
        
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double HueToRgb(double p, double q, double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0/6) return p + (q - p) * 6 * t;
                if (t < 1.0/2) return q;
                if (t < 2.0/3) return p + (q - p) * (2.0/3 - t) * 6;
                return p;
            }
            
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h/360 + 1.0/3);
            g = HueToRgb(p, q, h/360);
            b = HueToRgb(p, q, h/360 - 1.0/3);
        }
        
        return $"#{(int)(r * 255):X2}{(int)(g * 255):X2}{(int)(b * 255):X2}";
    }

    private class DallEResponse
    {
        public List<DallEImage>? Data { get; set; }
    }

    private class DallEImage
    {
        public string? Url { get; set; }
    }

    private class StableDiffusionResponse
    {
        public List<string>? Images { get; set; }
    }
}
