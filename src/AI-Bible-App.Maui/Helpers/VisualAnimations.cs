namespace AI_Bible_App.Maui.Helpers;

/// <summary>
/// Collection of beautiful animations for the Bible app
/// </summary>
public static class VisualAnimations
{
    /// <summary>
    /// Smooth entrance animation from bottom
    /// </summary>
    public static async Task SlideInFromBottom(VisualElement element, uint duration = 400, uint delay = 0)
    {
        if (delay > 0) await Task.Delay((int)delay);
        
        element.TranslationY = 50;
        element.Opacity = 0;
        element.IsVisible = true;
        
        await Task.WhenAll(
            element.TranslateTo(0, 0, duration, Easing.CubicOut),
            element.FadeTo(1, duration, Easing.CubicOut)
        );
    }

    /// <summary>
    /// Smooth entrance animation from right
    /// </summary>
    public static async Task SlideInFromRight(VisualElement element, uint duration = 400, uint delay = 0)
    {
        if (delay > 0) await Task.Delay((int)delay);
        
        element.TranslationX = 100;
        element.Opacity = 0;
        element.IsVisible = true;
        
        await Task.WhenAll(
            element.TranslateTo(0, 0, duration, Easing.CubicOut),
            element.FadeTo(1, duration, Easing.CubicOut)
        );
    }

    /// <summary>
    /// Smooth entrance animation from left
    /// </summary>
    public static async Task SlideInFromLeft(VisualElement element, uint duration = 400, uint delay = 0)
    {
        if (delay > 0) await Task.Delay((int)delay);
        
        element.TranslationX = -100;
        element.Opacity = 0;
        element.IsVisible = true;
        
        await Task.WhenAll(
            element.TranslateTo(0, 0, duration, Easing.CubicOut),
            element.FadeTo(1, duration, Easing.CubicOut)
        );
    }

    /// <summary>
    /// Scale and fade in animation
    /// </summary>
    public static async Task PopIn(VisualElement element, uint duration = 300, uint delay = 0)
    {
        if (delay > 0) await Task.Delay((int)delay);
        
        element.Scale = 0.5;
        element.Opacity = 0;
        element.IsVisible = true;
        
        await Task.WhenAll(
            element.ScaleTo(1, duration, Easing.SpringOut),
            element.FadeTo(1, duration / 2, Easing.CubicOut)
        );
    }

    /// <summary>
    /// Gentle bounce animation
    /// </summary>
    public static async Task Bounce(VisualElement element, double intensity = 0.1)
    {
        await element.ScaleTo(1 + intensity, 100, Easing.CubicOut);
        await element.ScaleTo(1, 100, Easing.BounceOut);
    }

    /// <summary>
    /// Pulse animation (good for highlighting)
    /// </summary>
    public static async Task Pulse(VisualElement element, int count = 2)
    {
        for (int i = 0; i < count; i++)
        {
            await element.ScaleTo(1.05, 150, Easing.SinInOut);
            await element.ScaleTo(1.0, 150, Easing.SinInOut);
        }
    }

    /// <summary>
    /// Shake animation (for errors or attention)
    /// </summary>
    public static async Task Shake(VisualElement element, double intensity = 10)
    {
        await element.TranslateTo(-intensity, 0, 50);
        await element.TranslateTo(intensity, 0, 50);
        await element.TranslateTo(-intensity / 2, 0, 50);
        await element.TranslateTo(intensity / 2, 0, 50);
        await element.TranslateTo(0, 0, 50);
    }

    /// <summary>
    /// Glow effect animation
    /// </summary>
    public static async Task GlowPulse(VisualElement element, Color glowColor, int count = 2)
    {
        var originalShadow = element.Shadow;
        
        for (int i = 0; i < count; i++)
        {
            element.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(glowColor),
                Offset = new Point(0, 0),
                Radius = 20,
                Opacity = 0.8f
            };
            await Task.Delay(200);
            
            element.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(glowColor),
                Offset = new Point(0, 0),
                Radius = 10,
                Opacity = 0.4f
            };
            await Task.Delay(200);
        }
        
        element.Shadow = originalShadow;
    }

    /// <summary>
    /// Typewriter text animation
    /// </summary>
    public static async Task TypewriterEffect(Label label, string text, int delayPerChar = 30)
    {
        label.Text = "";
        
        foreach (char c in text)
        {
            label.Text += c;
            await Task.Delay(delayPerChar);
        }
    }

    /// <summary>
    /// Staggered animation for collections
    /// </summary>
    public static async Task StaggeredEntrance(IEnumerable<VisualElement> elements, uint delayBetween = 50)
    {
        uint delay = 0;
        var tasks = new List<Task>();
        
        foreach (var element in elements)
        {
            tasks.Add(SlideInFromBottom(element, delay: delay));
            delay += delayBetween;
        }
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Card flip animation
    /// </summary>
    public static async Task FlipCard(VisualElement front, VisualElement back)
    {
        await front.RotateYTo(90, 150, Easing.CubicIn);
        front.IsVisible = false;
        
        back.RotationY = -90;
        back.IsVisible = true;
        await back.RotateYTo(0, 150, Easing.CubicOut);
    }

    /// <summary>
    /// Breathing animation (subtle scale pulsing)
    /// </summary>
    public static async Task StartBreathingAnimation(VisualElement element, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await element.ScaleTo(1.02, 2000, Easing.SinInOut);
            await element.ScaleTo(1.0, 2000, Easing.SinInOut);
        }
    }

    /// <summary>
    /// Fade and slide out
    /// </summary>
    public static async Task SlideOutToBottom(VisualElement element, uint duration = 300)
    {
        await Task.WhenAll(
            element.TranslateTo(0, 50, duration, Easing.CubicIn),
            element.FadeTo(0, duration, Easing.CubicIn)
        );
        element.IsVisible = false;
    }

    /// <summary>
    /// Ripple effect simulation
    /// </summary>
    public static async Task RippleEffect(VisualElement element)
    {
        var originalScale = element.Scale;
        
        await element.ScaleTo(originalScale * 0.95, 50);
        await element.ScaleTo(originalScale * 1.02, 100, Easing.CubicOut);
        await element.ScaleTo(originalScale, 100, Easing.CubicIn);
    }

    /// <summary>
    /// Success checkmark animation
    /// </summary>
    public static async Task SuccessAnimation(VisualElement element)
    {
        await element.ScaleTo(0, 0);
        element.IsVisible = true;
        
        await element.ScaleTo(1.2, 200, Easing.CubicOut);
        await element.ScaleTo(1.0, 100, Easing.BounceOut);
    }

    /// <summary>
    /// Loading dots animation
    /// </summary>
    public static async Task AnimateLoadingDots(Label dotsLabel, CancellationToken cancellationToken)
    {
        var dots = new[] { ".", "..", "...", "...." };
        int index = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            dotsLabel.Text = dots[index % dots.Length];
            index++;
            await Task.Delay(400);
        }
    }

    /// <summary>
    /// Floating animation (up and down)
    /// </summary>
    public static async Task StartFloatingAnimation(VisualElement element, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await element.TranslateTo(0, -5, 1500, Easing.SinInOut);
            await element.TranslateTo(0, 5, 1500, Easing.SinInOut);
        }
    }

    /// <summary>
    /// Highlight flash effect
    /// </summary>
    public static async Task FlashHighlight(VisualElement element, Color highlightColor)
    {
        var originalBg = element.BackgroundColor;
        
        element.BackgroundColor = highlightColor;
        await Task.Delay(100);
        
        // Fade back
        for (int i = 0; i < 5; i++)
        {
            element.Opacity = 1 - (i * 0.15);
            await Task.Delay(50);
        }
        
        element.BackgroundColor = originalBg;
        element.Opacity = 1;
    }
}
