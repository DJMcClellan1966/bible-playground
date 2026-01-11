using AI_Bible_App.Core.Interfaces;

namespace AI_Bible_App.Maui.Services;

/// <summary>
/// Service for managing dynamic font scaling across the application.
/// Provides font size multipliers based on user preference.
/// </summary>
public interface IFontScaleService
{
    /// <summary>
    /// Current font scale multiplier (1.0 = 100%, 1.25 = 125%, etc.)
    /// </summary>
    double Scale { get; }
    
    /// <summary>
    /// Current preference name (Small, Medium, Large, Extra Large)
    /// </summary>
    string CurrentPreference { get; }
    
    /// <summary>
    /// Apply font scale from user preference
    /// </summary>
    void ApplyScale(string preference);
    
    /// <summary>
    /// Get scaled font size
    /// </summary>
    double GetScaledSize(double baseSize);
    
    /// <summary>
    /// Event fired when scale changes
    /// </summary>
    event EventHandler<double>? ScaleChanged;
}

public class FontScaleService : IFontScaleService
{
    private readonly IUserService _userService;
    private double _scale = 1.0;
    private string _currentPreference = "Medium";

    public double Scale => _scale;
    public string CurrentPreference => _currentPreference;
    
    public event EventHandler<double>? ScaleChanged;

    // Base font sizes used throughout the app
    public static class BaseSizes
    {
        public const double Caption = 10;
        public const double Body = 14;
        public const double Subtitle = 16;
        public const double Title = 18;
        public const double Header = 24;
        public const double Display = 28;
    }

    public FontScaleService(IUserService userService)
    {
        _userService = userService;
        
        // Subscribe to user changes to update font scale
        _userService.CurrentUserChanged += (s, user) =>
        {
            if (user != null)
            {
                ApplyScale(user.Settings.FontSizePreference);
            }
        };
        
        // Apply initial scale from current user
        if (_userService.CurrentUser != null)
        {
            ApplyScale(_userService.CurrentUser.Settings.FontSizePreference);
        }
    }

    public void ApplyScale(string preference)
    {
        _currentPreference = preference;
        _scale = preference switch
        {
            "Small" => 0.85,
            "Medium" => 1.0,
            "Large" => 1.15,
            "Extra Large" => 1.3,
            _ => 1.0
        };

        // Update application resources with scaled font sizes
        UpdateApplicationResources();
        
        ScaleChanged?.Invoke(this, _scale);
        
        System.Diagnostics.Debug.WriteLine($"[FontScale] Applied scale: {preference} ({_scale}x)");
    }

    public double GetScaledSize(double baseSize)
    {
        return Math.Round(baseSize * _scale);
    }

    private void UpdateApplicationResources()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current?.Resources == null) return;

            var resources = Application.Current.Resources;

            // Update scaled font size resources
            resources["ScaledCaptionSize"] = GetScaledSize(BaseSizes.Caption);
            resources["ScaledBodySize"] = GetScaledSize(BaseSizes.Body);
            resources["ScaledSubtitleSize"] = GetScaledSize(BaseSizes.Subtitle);
            resources["ScaledTitleSize"] = GetScaledSize(BaseSizes.Title);
            resources["ScaledHeaderSize"] = GetScaledSize(BaseSizes.Header);
            resources["ScaledDisplaySize"] = GetScaledSize(BaseSizes.Display);
            
            // Additional common sizes
            resources["ScaledSize10"] = GetScaledSize(10);
            resources["ScaledSize11"] = GetScaledSize(11);
            resources["ScaledSize12"] = GetScaledSize(12);
            resources["ScaledSize13"] = GetScaledSize(13);
            resources["ScaledSize14"] = GetScaledSize(14);
            resources["ScaledSize15"] = GetScaledSize(15);
            resources["ScaledSize16"] = GetScaledSize(16);
            resources["ScaledSize18"] = GetScaledSize(18);
            resources["ScaledSize20"] = GetScaledSize(20);
            resources["ScaledSize24"] = GetScaledSize(24);
            resources["ScaledSize28"] = GetScaledSize(28);
            resources["ScaledSize32"] = GetScaledSize(32);

            System.Diagnostics.Debug.WriteLine($"[FontScale] Updated {12} scaled font resources");
        });
    }
}
