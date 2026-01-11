namespace AI_Bible_App.Maui.Controls;

/// <summary>
/// A beautiful, spiritually-themed loading indicator with animated rings
/// </summary>
public partial class SpiritualLoadingIndicator : ContentView
{
    private CancellationTokenSource? _animationCts;
    private bool _isAnimating;
    
    public static readonly BindableProperty LoadingMessageProperty = BindableProperty.Create(
        nameof(LoadingMessage),
        typeof(string),
        typeof(SpiritualLoadingIndicator),
        "Seeking wisdom...",
        propertyChanged: OnLoadingMessageChanged);
    
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon),
        typeof(string),
        typeof(SpiritualLoadingIndicator),
        "✝",
        propertyChanged: OnIconChanged);
    
    public static readonly BindableProperty IsAnimatingProperty = BindableProperty.Create(
        nameof(IsAnimating),
        typeof(bool),
        typeof(SpiritualLoadingIndicator),
        false,
        propertyChanged: OnIsAnimatingChanged);
    
    public string LoadingMessage
    {
        get => (string)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }
    
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    
    public bool IsAnimating
    {
        get => (bool)GetValue(IsAnimatingProperty);
        set => SetValue(IsAnimatingProperty, value);
    }
    
    public SpiritualLoadingIndicator()
    {
        InitializeComponent();
    }
    
    private static void OnLoadingMessageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SpiritualLoadingIndicator indicator && newValue is string message)
        {
            indicator.LoadingText.Text = message;
        }
    }
    
    private static void OnIconChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SpiritualLoadingIndicator indicator && newValue is string icon)
        {
            indicator.CenterIcon.Text = icon;
        }
    }
    
    private static void OnIsAnimatingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SpiritualLoadingIndicator indicator && newValue is bool isAnimating)
        {
            if (isAnimating)
            {
                indicator.StartAnimation();
            }
            else
            {
                indicator.StopAnimation();
            }
        }
    }
    
    public void StartAnimation()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;
        
        // Start all animations
        _ = AnimateOuterRing(token);
        _ = AnimateMiddleRing(token);
        _ = AnimateInnerPulse(token);
        _ = AnimateIcon(token);
        _ = AnimateDots(token);
    }
    
    public void StopAnimation()
    {
        _isAnimating = false;
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;
    }
    
    private async Task AnimateOuterRing(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await OuterRing.ScaleTo(1.1, 1500, Easing.SinInOut);
                await OuterRing.ScaleTo(1.0, 1500, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException) { }
    }
    
    private async Task AnimateMiddleRing(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await MiddleRing.RotateTo(360, 3000, Easing.Linear);
                MiddleRing.Rotation = 0;
            }
        }
        catch (TaskCanceledException) { }
    }
    
    private async Task AnimateInnerPulse(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await InnerCircle.FadeTo(0.4, 800, Easing.SinInOut);
                await InnerCircle.FadeTo(0.8, 800, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException) { }
    }
    
    private async Task AnimateIcon(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await CenterIcon.ScaleTo(1.1, 600, Easing.SinInOut);
                await CenterIcon.ScaleTo(1.0, 600, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException) { }
    }
    
    private async Task AnimateDots(CancellationToken token)
    {
        var patterns = new[] { "", ".", "..", "...", "...." };
        int index = 0;
        
        try
        {
            while (!token.IsCancellationRequested)
            {
                DotsLabel.Text = patterns[index % patterns.Length];
                index++;
                await Task.Delay(300, token);
            }
        }
        catch (TaskCanceledException) { }
    }
    
    /// <summary>
    /// Set message with contextual loading phrases
    /// </summary>
    public void SetContextualMessage(LoadingContext context)
    {
        LoadingMessage = context switch
        {
            LoadingContext.GeneratingResponse => "Contemplating scripture...",
            LoadingContext.SearchingBible => "Searching the Word...",
            LoadingContext.LoadingCharacter => "Gathering wisdom...",
            LoadingContext.StartingDiscussion => "Preparing the roundtable...",
            LoadingContext.ProcessingQuestion => "Seeking understanding...",
            LoadingContext.LoadingHistory => "Recalling conversations...",
            LoadingContext.GeneratingImage => "Creating vision...",
            _ => "Please wait..."
        };
        
        Icon = context switch
        {
            LoadingContext.SearchingBible => "📖",
            LoadingContext.StartingDiscussion => "👥",
            LoadingContext.GeneratingImage => "🎨",
            _ => "✝"
        };
    }
    
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        if (Handler == null)
        {
            StopAnimation();
        }
    }
}

public enum LoadingContext
{
    Default,
    GeneratingResponse,
    SearchingBible,
    LoadingCharacter,
    StartingDiscussion,
    ProcessingQuestion,
    LoadingHistory,
    GeneratingImage
}
