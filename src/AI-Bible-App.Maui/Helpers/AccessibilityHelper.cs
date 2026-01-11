using Microsoft.Maui.Controls;

namespace AI_Bible_App.Maui.Helpers;

/// <summary>
/// Accessibility helper extensions for MAUI controls.
/// Provides methods to set semantic properties for screen readers.
/// </summary>
public static class AccessibilityHelper
{
    /// <summary>
    /// Sets the heading level for a label to help screen readers understand document structure.
    /// HeadingLevel values: None=0, Level1=1, Level2=2, Level3=3, Level4=4, Level5=5, Level6=6
    /// </summary>
    public static T WithHeadingLevel<T>(this T element, SemanticHeadingLevel level) where T : VisualElement
    {
        SemanticProperties.SetHeadingLevel(element, level);
        return element;
    }

    /// <summary>
    /// Sets the description for a control that screen readers will announce.
    /// </summary>
    public static T WithDescription<T>(this T element, string description) where T : VisualElement
    {
        SemanticProperties.SetDescription(element, description);
        return element;
    }

    /// <summary>
    /// Sets a hint for a control that provides additional context to screen readers.
    /// </summary>
    public static T WithHint<T>(this T element, string hint) where T : VisualElement
    {
        SemanticProperties.SetHint(element, hint);
        return element;
    }

    /// <summary>
    /// Makes an element announce changes to screen readers (live region).
    /// </summary>
    public static void AnnounceChange(string message)
    {
        // Use MAUI's built-in announcement system
        SemanticScreenReader.Announce(message);
    }

    /// <summary>
    /// Announces with a polite priority (doesn't interrupt current speech).
    /// </summary>
    public static void AnnouncePolite(string message)
    {
        // In MAUI, we use the default Announce which is polite
        SemanticScreenReader.Announce(message);
    }

    /// <summary>
    /// Creates accessibility-friendly page heading.
    /// </summary>
    public static Label CreatePageHeading(string text, string? description = null)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level1);
        SemanticProperties.SetDescription(label, description ?? text);
        
        return label;
    }

    /// <summary>
    /// Creates accessibility-friendly section heading.
    /// </summary>
    public static Label CreateSectionHeading(string text, int level = 2)
    {
        var label = new Label
        {
            Text = text,
            FontSize = level == 2 ? 22 : (level == 3 ? 18 : 16),
            FontAttributes = FontAttributes.Bold
        };
        
        var headingLevel = level switch
        {
            1 => SemanticHeadingLevel.Level1,
            2 => SemanticHeadingLevel.Level2,
            3 => SemanticHeadingLevel.Level3,
            4 => SemanticHeadingLevel.Level4,
            5 => SemanticHeadingLevel.Level5,
            6 => SemanticHeadingLevel.Level6,
            _ => SemanticHeadingLevel.Level2
        };
        
        SemanticProperties.SetHeadingLevel(label, headingLevel);
        
        return label;
    }

    /// <summary>
    /// Sets focus to the specified element with a slight delay to ensure the UI is ready.
    /// </summary>
    public static async Task SetFocusAsync(VisualElement element, int delayMs = 100)
    {
        await Task.Delay(delayMs);
        element.Focus();
    }

    /// <summary>
    /// Announces a loading state change to screen readers.
    /// </summary>
    public static void AnnounceLoading(bool isLoading, string? customMessage = null)
    {
        var message = customMessage ?? (isLoading ? "Loading, please wait" : "Content loaded");
        SemanticScreenReader.Announce(message);
    }

    /// <summary>
    /// Announces an error to screen readers with appropriate urgency.
    /// </summary>
    public static void AnnounceError(string errorMessage)
    {
        SemanticScreenReader.Announce($"Error: {errorMessage}");
    }

    /// <summary>
    /// Announces a success message to screen readers.
    /// </summary>
    public static void AnnounceSuccess(string successMessage)
    {
        SemanticScreenReader.Announce(successMessage);
    }

    /// <summary>
    /// Announces navigation to a new page/section.
    /// </summary>
    public static void AnnouncePageNavigation(string pageName)
    {
        SemanticScreenReader.Announce($"Navigated to {pageName}");
    }

    /// <summary>
    /// Announces new content (like a new chat message).
    /// </summary>
    public static void AnnounceNewContent(string contentType, string? preview = null)
    {
        var message = string.IsNullOrEmpty(preview)
            ? $"New {contentType}"
            : $"New {contentType}: {preview}";
        SemanticScreenReader.Announce(message);
    }
}

/// <summary>
/// Attached properties for setting semantic properties in XAML.
/// </summary>
public static class A11y
{
    // HeadingLevel attached property for XAML
    public static readonly BindableProperty HeadingLevelProperty =
        BindableProperty.CreateAttached(
            "HeadingLevel",
            typeof(SemanticHeadingLevel),
            typeof(A11y),
            SemanticHeadingLevel.None,
            propertyChanged: OnHeadingLevelChanged);

    public static SemanticHeadingLevel GetHeadingLevel(BindableObject view) =>
        (SemanticHeadingLevel)view.GetValue(HeadingLevelProperty);

    public static void SetHeadingLevel(BindableObject view, SemanticHeadingLevel value) =>
        view.SetValue(HeadingLevelProperty, value);

    private static void OnHeadingLevelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement element && newValue is SemanticHeadingLevel level)
        {
            SemanticProperties.SetHeadingLevel(element, level);
        }
    }

    // Hint attached property for XAML
    public static readonly BindableProperty HintProperty =
        BindableProperty.CreateAttached(
            "Hint",
            typeof(string),
            typeof(A11y),
            string.Empty,
            propertyChanged: OnHintChanged);

    public static string GetHint(BindableObject view) =>
        (string)view.GetValue(HintProperty);

    public static void SetHint(BindableObject view, string value) =>
        view.SetValue(HintProperty, value);

    private static void OnHintChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement element && newValue is string hint)
        {
            SemanticProperties.SetHint(element, hint);
        }
    }

    // IsLiveRegion attached property for announcing dynamic content changes
    public static readonly BindableProperty IsLiveRegionProperty =
        BindableProperty.CreateAttached(
            "IsLiveRegion",
            typeof(bool),
            typeof(A11y),
            false);

    public static bool GetIsLiveRegion(BindableObject view) =>
        (bool)view.GetValue(IsLiveRegionProperty);

    public static void SetIsLiveRegion(BindableObject view, bool value) =>
        view.SetValue(IsLiveRegionProperty, value);
}
