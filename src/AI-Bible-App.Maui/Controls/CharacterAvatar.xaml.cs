using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Maui.Controls;

public partial class CharacterAvatar : ContentView
{
    private static readonly Dictionary<string, (Color Primary, Color Secondary)> CharacterColors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Old Testament - Earth and Royal tones
        {"moses", (Color.FromArgb("#8B4513"), Color.FromArgb("#D4AF37"))},
        {"david", (Color.FromArgb("#4169E1"), Color.FromArgb("#FFD700"))},
        {"abraham", (Color.FromArgb("#8B7355"), Color.FromArgb("#F5DEB3"))},
        {"solomon", (Color.FromArgb("#800080"), Color.FromArgb("#FFD700"))},
        {"elijah", (Color.FromArgb("#DC143C"), Color.FromArgb("#FF6347"))},
        {"isaiah", (Color.FromArgb("#4B0082"), Color.FromArgb("#E6E6FA"))},
        {"daniel", (Color.FromArgb("#000080"), Color.FromArgb("#C0C0C0"))},
        {"joseph", (Color.FromArgb("#FFD700"), Color.FromArgb("#800080"))},
        {"ruth", (Color.FromArgb("#DDA0DD"), Color.FromArgb("#F5DEB3"))},
        {"esther", (Color.FromArgb("#FF69B4"), Color.FromArgb("#FFD700"))},
        {"job", (Color.FromArgb("#696969"), Color.FromArgb("#F0E68C"))},
        {"noah", (Color.FromArgb("#4682B4"), Color.FromArgb("#8B4513"))},
        {"jeremiah", (Color.FromArgb("#2F4F4F"), Color.FromArgb("#708090"))},
        {"ezekiel", (Color.FromArgb("#9932CC"), Color.FromArgb("#00CED1"))},
        {"samuel", (Color.FromArgb("#556B2F"), Color.FromArgb("#BDB76B"))},
        {"joshua", (Color.FromArgb("#8B0000"), Color.FromArgb("#CD853F"))},
        {"gideon", (Color.FromArgb("#A0522D"), Color.FromArgb("#DAA520"))},
        {"samson", (Color.FromArgb("#B8860B"), Color.FromArgb("#8B4513"))},
        {"nehemiah", (Color.FromArgb("#2E8B57"), Color.FromArgb("#3CB371"))},
        {"ezra", (Color.FromArgb("#4682B4"), Color.FromArgb("#87CEEB"))},
        
        // New Testament - Lighter, spiritual tones
        {"jesus", (Color.FromArgb("#FFFAF0"), Color.FromArgb("#FFD700"))},
        {"peter", (Color.FromArgb("#1E90FF"), Color.FromArgb("#A9A9A9"))},
        {"paul", (Color.FromArgb("#8B0000"), Color.FromArgb("#D2691E"))},
        {"john", (Color.FromArgb("#FF4500"), Color.FromArgb("#FFFAF0"))},
        {"mary", (Color.FromArgb("#87CEEB"), Color.FromArgb("#FFFFFF"))},
        {"martha", (Color.FromArgb("#D2B48C"), Color.FromArgb("#F5F5DC"))},
        {"marymagdalene", (Color.FromArgb("#9370DB"), Color.FromArgb("#FFB6C1"))},
        {"thomas", (Color.FromArgb("#A52A2A"), Color.FromArgb("#F4A460"))},
        {"james", (Color.FromArgb("#228B22"), Color.FromArgb("#F0FFF0"))},
        {"luke", (Color.FromArgb("#20B2AA"), Color.FromArgb("#FAFAD2"))},
        {"mark", (Color.FromArgb("#B22222"), Color.FromArgb("#FFDAB9"))},
        {"matthew", (Color.FromArgb("#DAA520"), Color.FromArgb("#FFFACD"))},
        {"barnabas", (Color.FromArgb("#32CD32"), Color.FromArgb("#FFFAF0"))},
        {"timothy", (Color.FromArgb("#6B8E23"), Color.FromArgb("#F5F5DC"))},
        {"stephen", (Color.FromArgb("#FF6347"), Color.FromArgb("#FFFAFA"))},
        {"priscilla", (Color.FromArgb("#DB7093"), Color.FromArgb("#FFF0F5"))},
        {"andrew", (Color.FromArgb("#4169E1"), Color.FromArgb("#B0C4DE"))},
        {"philip", (Color.FromArgb("#9ACD32"), Color.FromArgb("#FAFAD2"))},
        {"nathanael", (Color.FromArgb("#6A5ACD"), Color.FromArgb("#E6E6FA"))},
        {"nicodemus", (Color.FromArgb("#191970"), Color.FromArgb("#4169E1"))},
    };

    public CharacterAvatar()
    {
        InitializeComponent();
    }

    #region Bindable Properties

    public static readonly BindableProperty CharacterProperty = BindableProperty.Create(
        nameof(Character), typeof(BiblicalCharacter), typeof(CharacterAvatar),
        propertyChanged: OnCharacterChanged);

    public static readonly BindableProperty CharacterNameProperty = BindableProperty.Create(
        nameof(CharacterName), typeof(string), typeof(CharacterAvatar),
        propertyChanged: OnCharacterNameChanged);

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(CharacterAvatar), 60.0,
        propertyChanged: OnSizeChanged);

    public static readonly BindableProperty IsSpeakingProperty = BindableProperty.Create(
        nameof(IsSpeaking), typeof(bool), typeof(CharacterAvatar), false,
        propertyChanged: OnIsSpeakingChanged);

    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(
        nameof(ImageSource), typeof(ImageSource), typeof(CharacterAvatar));

    public static readonly BindableProperty PrimaryColorProperty = BindableProperty.Create(
        nameof(PrimaryColor), typeof(Color), typeof(CharacterAvatar), Color.FromArgb("#4169E1"));

    public static readonly BindableProperty SecondaryColorProperty = BindableProperty.Create(
        nameof(SecondaryColor), typeof(Color), typeof(CharacterAvatar), Color.FromArgb("#FFD700"));

    public static readonly BindableProperty BorderColorStartProperty = BindableProperty.Create(
        nameof(BorderColorStart), typeof(Color), typeof(CharacterAvatar), Color.FromArgb("#FFD700"));

    public static readonly BindableProperty BorderColorEndProperty = BindableProperty.Create(
        nameof(BorderColorEnd), typeof(Color), typeof(CharacterAvatar), Color.FromArgb("#FFA500"));

    public static readonly BindableProperty InitialsProperty = BindableProperty.Create(
        nameof(Initials), typeof(string), typeof(CharacterAvatar), "?");

    public static readonly BindableProperty InitialsFontSizeProperty = BindableProperty.Create(
        nameof(InitialsFontSize), typeof(double), typeof(CharacterAvatar), 24.0);

    public static readonly BindableProperty ShowGradientProperty = BindableProperty.Create(
        nameof(ShowGradient), typeof(bool), typeof(CharacterAvatar), true);

    public static readonly BindableProperty ShowImageProperty = BindableProperty.Create(
        nameof(ShowImage), typeof(bool), typeof(CharacterAvatar), false);

    public static readonly BindableProperty ShowGlowProperty = BindableProperty.Create(
        nameof(ShowGlow), typeof(bool), typeof(CharacterAvatar), false);

    #endregion

    #region Properties

    public BiblicalCharacter? Character
    {
        get => (BiblicalCharacter?)GetValue(CharacterProperty);
        set => SetValue(CharacterProperty, value);
    }

    public string? CharacterName
    {
        get => (string?)GetValue(CharacterNameProperty);
        set => SetValue(CharacterNameProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool IsSpeaking
    {
        get => (bool)GetValue(IsSpeakingProperty);
        set => SetValue(IsSpeakingProperty, value);
    }

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public Color PrimaryColor
    {
        get => (Color)GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public Color SecondaryColor
    {
        get => (Color)GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    public Color BorderColorStart
    {
        get => (Color)GetValue(BorderColorStartProperty);
        set => SetValue(BorderColorStartProperty, value);
    }

    public Color BorderColorEnd
    {
        get => (Color)GetValue(BorderColorEndProperty);
        set => SetValue(BorderColorEndProperty, value);
    }

    public string Initials
    {
        get => (string)GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }

    public double InitialsFontSize
    {
        get => (double)GetValue(InitialsFontSizeProperty);
        set => SetValue(InitialsFontSizeProperty, value);
    }

    public bool ShowGradient
    {
        get => (bool)GetValue(ShowGradientProperty);
        set => SetValue(ShowGradientProperty, value);
    }

    public bool ShowImage
    {
        get => (bool)GetValue(ShowImageProperty);
        set => SetValue(ShowImageProperty, value);
    }

    public bool ShowGlow
    {
        get => (bool)GetValue(ShowGlowProperty);
        set => SetValue(ShowGlowProperty, value);
    }

    #endregion

    #region Property Changed Handlers

    private static void OnCharacterChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CharacterAvatar avatar && newValue is BiblicalCharacter character)
        {
            avatar.UpdateFromCharacter(character.Name);
        }
    }

    private static void OnCharacterNameChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CharacterAvatar avatar && newValue is string name)
        {
            avatar.UpdateFromCharacter(name);
        }
    }

    private static void OnSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CharacterAvatar avatar && newValue is double size)
        {
            avatar.InitialsFontSize = size * 0.4;
        }
    }

    private static async void OnIsSpeakingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CharacterAvatar avatar && newValue is bool isSpeaking)
        {
            await avatar.AnimateSpeakingAsync(isSpeaking);
        }
    }

    #endregion

    private void UpdateFromCharacter(string name)
    {
        var normalizedName = name.ToLower().Replace(" ", "").Replace("magdalene", "");
        
        // Get colors
        var (primary, secondary) = GetColorsForCharacter(normalizedName);
        PrimaryColor = primary;
        SecondaryColor = secondary;
        BorderColorStart = secondary;
        BorderColorEnd = primary;
        
        // Generate initials
        Initials = GetInitials(name);
    }

    private static (Color Primary, Color Secondary) GetColorsForCharacter(string name)
    {
        if (CharacterColors.TryGetValue(name, out var colors))
            return colors;
        
        // Generate consistent colors based on name hash
        var hash = Math.Abs(name.GetHashCode());
        var hue = hash % 360;
        var primary = Color.FromHsla(hue / 360.0, 0.6, 0.4);
        var secondary = Color.FromHsla(((hue + 40) % 360) / 360.0, 0.5, 0.6);
        
        return (primary, secondary);
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        return name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
    }

    private async Task AnimateSpeakingAsync(bool isSpeaking)
    {
        if (isSpeaking)
        {
            // Pulse animation for speaking indicator
            SpeakingRing.IsVisible = true;
            
            while (IsSpeaking)
            {
                await SpeakingRing.FadeTo(0.9, 400, Easing.SinInOut);
                await SpeakingRing.FadeTo(0.3, 400, Easing.SinInOut);
            }
        }
        else
        {
            await SpeakingRing.FadeTo(0, 200);
            SpeakingRing.IsVisible = false;
        }
    }

    public async Task AnimateSelectionAsync()
    {
        await this.ScaleTo(1.1, 150, Easing.SpringOut);
        await this.ScaleTo(1.0, 150, Easing.SpringIn);
    }

    public async Task AnimateGlowAsync()
    {
        ShowGlow = true;
        await GlowOverlay.FadeTo(0.6, 300, Easing.CubicOut);
        await GlowOverlay.FadeTo(0, 500, Easing.CubicIn);
        ShowGlow = false;
    }
}
