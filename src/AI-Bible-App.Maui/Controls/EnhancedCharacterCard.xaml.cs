using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Maui.Controls;

public partial class EnhancedCharacterCard : ContentView
{
    public event EventHandler<BiblicalCharacter>? ChatRequested;
    public event EventHandler<BiblicalCharacter>? SelectionChanged;

    public EnhancedCharacterCard()
    {
        InitializeComponent();
    }

    #region Bindable Properties

    public static readonly BindableProperty CharacterProperty = BindableProperty.Create(
        nameof(Character), typeof(BiblicalCharacter), typeof(EnhancedCharacterCard),
        propertyChanged: OnCharacterChanged);

    public static readonly BindableProperty CharacterNameProperty = BindableProperty.Create(
        nameof(CharacterName), typeof(string), typeof(EnhancedCharacterCard), "Character");

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(EnhancedCharacterCard), "Biblical Figure");

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(EnhancedCharacterCard), "");

    public static readonly BindableProperty EraProperty = BindableProperty.Create(
        nameof(Era), typeof(string), typeof(EnhancedCharacterCard), "Biblical Era");

    public static readonly BindableProperty RoleTextProperty = BindableProperty.Create(
        nameof(RoleText), typeof(string), typeof(EnhancedCharacterCard), "");

    public static readonly BindableProperty BooksCountProperty = BindableProperty.Create(
        nameof(BooksCount), typeof(string), typeof(EnhancedCharacterCard), "0 books");

    public static readonly BindableProperty PrimaryColorProperty = BindableProperty.Create(
        nameof(PrimaryColor), typeof(Color), typeof(EnhancedCharacterCard), Color.FromArgb("#4169E1"));

    public static readonly BindableProperty SecondaryColorProperty = BindableProperty.Create(
        nameof(SecondaryColor), typeof(Color), typeof(EnhancedCharacterCard), Color.FromArgb("#FFD700"));

    public static readonly BindableProperty BorderColorStartProperty = BindableProperty.Create(
        nameof(BorderColorStart), typeof(Color), typeof(EnhancedCharacterCard), Color.FromArgb("#FFD700"));

    public static readonly BindableProperty BorderColorEndProperty = BindableProperty.Create(
        nameof(BorderColorEnd), typeof(Color), typeof(EnhancedCharacterCard), Color.FromArgb("#FFA500"));

    public static readonly BindableProperty ShadowColorProperty = BindableProperty.Create(
        nameof(ShadowColor), typeof(Color), typeof(EnhancedCharacterCard), Color.FromArgb("#000000"));

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(EnhancedCharacterCard), false,
        propertyChanged: OnIsSelectedChanged);

    public static readonly BindableProperty IsSelectableProperty = BindableProperty.Create(
        nameof(IsSelectable), typeof(bool), typeof(EnhancedCharacterCard), false);

    #endregion

    #region Properties

    public BiblicalCharacter? Character
    {
        get => (BiblicalCharacter?)GetValue(CharacterProperty);
        set => SetValue(CharacterProperty, value);
    }

    public string CharacterName
    {
        get => (string)GetValue(CharacterNameProperty);
        set => SetValue(CharacterNameProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Era
    {
        get => (string)GetValue(EraProperty);
        set => SetValue(EraProperty, value);
    }

    public string RoleText
    {
        get => (string)GetValue(RoleTextProperty);
        set => SetValue(RoleTextProperty, value);
    }

    public string BooksCount
    {
        get => (string)GetValue(BooksCountProperty);
        set => SetValue(BooksCountProperty, value);
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

    public Color ShadowColor
    {
        get => (Color)GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsSelectable
    {
        get => (bool)GetValue(IsSelectableProperty);
        set => SetValue(IsSelectableProperty, value);
    }

    #endregion

    private static void OnCharacterChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EnhancedCharacterCard card && newValue is BiblicalCharacter character)
        {
            card.UpdateFromCharacter(character);
        }
    }

    private static async void OnIsSelectedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EnhancedCharacterCard card && newValue is bool isSelected)
        {
            await card.AnimateSelectionAsync(isSelected);
        }
    }

    private void UpdateFromCharacter(BiblicalCharacter character)
    {
        CharacterName = character.Name;
        Title = character.Title ?? "Biblical Figure";
        Description = character.Description ?? "";
        Era = character.Era ?? "Biblical Era";
        
        // Determine role
        RoleText = DetermineRole(character);
        
        // Count books from BiblicalReferences
        var bookCount = character.BiblicalReferences?.Count ?? 0;
        BooksCount = bookCount == 1 ? "1 reference" : $"{bookCount} references";
        
        // Set colors based on character
        var colors = GetCharacterColors(character.Name);
        PrimaryColor = colors.Primary;
        SecondaryColor = colors.Secondary;
        BorderColorStart = colors.Secondary;
        BorderColorEnd = colors.Primary;
        ShadowColor = colors.Primary;
    }

    private string DetermineRole(BiblicalCharacter character)
    {
        var title = character.Title?.ToLower() ?? "";
        var name = character.Name.ToLower();
        
        if (title.Contains("prophet") || title.Contains("seer"))
            return "⚡ PROPHET";
        if (title.Contains("king") || title.Contains("ruler"))
            return "👑 ROYALTY";
        if (title.Contains("apostle"))
            return "✝️ APOSTLE";
        if (title.Contains("patriarch") || title.Contains("father"))
            return "🏛️ PATRIARCH";
        if (title.Contains("judge"))
            return "⚖️ JUDGE";
        if (title.Contains("priest"))
            return "🕯️ PRIEST";
        if (name.Contains("mary") || name.Contains("ruth") || name.Contains("esther"))
            return "💫 WOMAN OF FAITH";
        if (title.Contains("disciple"))
            return "📿 DISCIPLE";
        
        return "📜 BIBLICAL FIGURE";
    }

    private static (Color Primary, Color Secondary) GetCharacterColors(string name)
    {
        var normalizedName = name.ToLower().Replace(" ", "");
        
        // Character-specific colors
        var colorMap = new Dictionary<string, (Color, Color)>(StringComparer.OrdinalIgnoreCase)
        {
            {"moses", (Color.FromArgb("#8B4513"), Color.FromArgb("#D4AF37"))},
            {"david", (Color.FromArgb("#4169E1"), Color.FromArgb("#FFD700"))},
            {"abraham", (Color.FromArgb("#8B7355"), Color.FromArgb("#F5DEB3"))},
            {"solomon", (Color.FromArgb("#800080"), Color.FromArgb("#FFD700"))},
            {"elijah", (Color.FromArgb("#DC143C"), Color.FromArgb("#FF6347"))},
            {"peter", (Color.FromArgb("#1E90FF"), Color.FromArgb("#A9A9A9"))},
            {"paul", (Color.FromArgb("#8B0000"), Color.FromArgb("#D2691E"))},
            {"john", (Color.FromArgb("#FF4500"), Color.FromArgb("#FFFAF0"))},
            {"mary", (Color.FromArgb("#87CEEB"), Color.FromArgb("#FFFFFF"))},
            {"jesus", (Color.FromArgb("#FFFAF0"), Color.FromArgb("#FFD700"))},
            {"daniel", (Color.FromArgb("#000080"), Color.FromArgb("#C0C0C0"))},
            {"ruth", (Color.FromArgb("#DDA0DD"), Color.FromArgb("#F5DEB3"))},
            {"esther", (Color.FromArgb("#FF69B4"), Color.FromArgb("#FFD700"))},
            {"isaiah", (Color.FromArgb("#4B0082"), Color.FromArgb("#E6E6FA"))},
            {"jeremiah", (Color.FromArgb("#2F4F4F"), Color.FromArgb("#708090"))},
        };
        
        if (colorMap.TryGetValue(normalizedName, out var colors))
            return colors;
        
        // Generate colors from name hash
        var hash = Math.Abs(name.GetHashCode());
        var hue = hash % 360;
        var primary = Color.FromHsla(hue / 360.0, 0.6, 0.4);
        var secondary = Color.FromHsla(((hue + 40) % 360) / 360.0, 0.5, 0.6);
        
        return (primary, secondary);
    }

    private async Task AnimateSelectionAsync(bool selected)
    {
        if (selected)
        {
            SelectionIndicator.Opacity = 0;
            SelectionIndicator.IsVisible = true;
            
            await Task.WhenAll(
                SelectionIndicator.FadeTo(1, 200, Easing.CubicOut),
                CardBorder.ScaleTo(1.02, 150, Easing.SpringOut),
                Avatar.AnimateSelectionAsync()
            );
        }
        else
        {
            await Task.WhenAll(
                SelectionIndicator.FadeTo(0, 150, Easing.CubicIn),
                CardBorder.ScaleTo(1.0, 150, Easing.CubicIn)
            );
            SelectionIndicator.IsVisible = false;
        }
    }

    private void OnChatClicked(object? sender, EventArgs e)
    {
        if (Character != null)
        {
            ChatRequested?.Invoke(this, Character);
        }
    }

    public async Task AnimateHoverEnterAsync()
    {
        await CardBorder.ScaleTo(1.02, 100, Easing.CubicOut);
    }

    public async Task AnimateHoverExitAsync()
    {
        await CardBorder.ScaleTo(1.0, 100, Easing.CubicIn);
    }

    public async Task AnimateTapAsync()
    {
        await CardBorder.ScaleTo(0.97, 80, Easing.CubicIn);
        await CardBorder.ScaleTo(1.0, 80, Easing.CubicOut);
        
        if (IsSelectable)
        {
            IsSelected = !IsSelected;
            SelectionChanged?.Invoke(this, Character!);
        }
    }
}
