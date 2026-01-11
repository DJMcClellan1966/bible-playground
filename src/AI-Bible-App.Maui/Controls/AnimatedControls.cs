using Microsoft.Maui.Controls.Shapes;

namespace AI_Bible_App.Maui.Controls;

/// <summary>
/// Animated message bubble with smooth entrance animations,
/// typing indicators, and mood-based styling.
/// </summary>
public class AnimatedMessageBubble : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(AnimatedMessageBubble), 
            string.Empty, propertyChanged: OnMessageChanged);

    public static readonly BindableProperty SenderProperty =
        BindableProperty.Create(nameof(Sender), typeof(string), typeof(AnimatedMessageBubble), string.Empty);

    public static readonly BindableProperty IsFromUserProperty =
        BindableProperty.Create(nameof(IsFromUser), typeof(bool), typeof(AnimatedMessageBubble), 
            false, propertyChanged: OnStyleChanged);

    public static readonly BindableProperty MoodColorProperty =
        BindableProperty.Create(nameof(MoodColor), typeof(Color), typeof(AnimatedMessageBubble), 
            Colors.DodgerBlue, propertyChanged: OnStyleChanged);

    public static readonly BindableProperty IsTypingProperty =
        BindableProperty.Create(nameof(IsTyping), typeof(bool), typeof(AnimatedMessageBubble), 
            false, propertyChanged: OnTypingChanged);

    public static readonly BindableProperty AnimateEntranceProperty =
        BindableProperty.Create(nameof(AnimateEntrance), typeof(bool), typeof(AnimatedMessageBubble), true);

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string Sender
    {
        get => (string)GetValue(SenderProperty);
        set => SetValue(SenderProperty, value);
    }

    public bool IsFromUser
    {
        get => (bool)GetValue(IsFromUserProperty);
        set => SetValue(IsFromUserProperty, value);
    }

    public Color MoodColor
    {
        get => (Color)GetValue(MoodColorProperty);
        set => SetValue(MoodColorProperty, value);
    }

    public bool IsTyping
    {
        get => (bool)GetValue(IsTypingProperty);
        set => SetValue(IsTypingProperty, value);
    }

    public bool AnimateEntrance
    {
        get => (bool)GetValue(AnimateEntranceProperty);
        set => SetValue(AnimateEntranceProperty, value);
    }

    private readonly Border _bubble;
    private readonly Label _messageLabel;
    private readonly Label _senderLabel;
    private readonly HorizontalStackLayout _typingIndicator;
    private readonly BoxView[] _typingDots;
    private CancellationTokenSource? _typingCts;

    public AnimatedMessageBubble()
    {
        // Sender label
        _senderLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        };

        // Message label
        _messageLabel = new Label
        {
            FontSize = 16,
            LineBreakMode = LineBreakMode.WordWrap
        };

        // Typing indicator
        _typingDots = new BoxView[3];
        _typingIndicator = new HorizontalStackLayout
        {
            Spacing = 4,
            IsVisible = false,
            HeightRequest = 24,
            VerticalOptions = LayoutOptions.Center
        };

        for (int i = 0; i < 3; i++)
        {
            _typingDots[i] = new BoxView
            {
                Color = Colors.Gray,
                WidthRequest = 8,
                HeightRequest = 8,
                CornerRadius = 4
            };
            _typingIndicator.Children.Add(_typingDots[i]);
        }

        // Main content
        var contentStack = new VerticalStackLayout
        {
            Children = { _senderLabel, _messageLabel, _typingIndicator }
        };

        // Bubble border
        _bubble = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(16, 12),
            Content = contentStack,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) }
        };

        Content = _bubble;
        Opacity = 0;
        TranslationY = 20;
        
        UpdateStyle();
    }

    protected override async void OnParentSet()
    {
        base.OnParentSet();
        
        if (Parent != null && AnimateEntrance)
        {
            await Task.Delay(50); // Small delay for smoother animation
            await AnimateEntranceAsync();
        }
    }

    private async Task AnimateEntranceAsync()
    {
        var fadeTask = this.FadeTo(1, 300, Easing.CubicOut);
        var slideTask = this.TranslateTo(0, 0, 300, Easing.CubicOut);
        var scaleTask = this.ScaleTo(1, 250, Easing.SpringOut);
        
        Scale = 0.8;
        await Task.WhenAll(fadeTask, slideTask, scaleTask);
    }

    private static void OnMessageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AnimatedMessageBubble bubble)
        {
            bubble._messageLabel.Text = newValue?.ToString() ?? "";
        }
    }

    private static void OnStyleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AnimatedMessageBubble bubble)
        {
            bubble.UpdateStyle();
        }
    }

    private static void OnTypingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AnimatedMessageBubble bubble)
        {
            var isTyping = (bool)newValue;
            bubble._typingIndicator.IsVisible = isTyping;
            bubble._messageLabel.IsVisible = !isTyping;
            
            if (isTyping)
            {
                bubble.StartTypingAnimation();
            }
            else
            {
                bubble.StopTypingAnimation();
            }
        }
    }

    private void UpdateStyle()
    {
        if (IsFromUser)
        {
            _bubble.BackgroundColor = Color.FromArgb("#2563EB");
            _messageLabel.TextColor = Colors.White;
            _senderLabel.TextColor = Color.FromArgb("#BFDBFE");
            HorizontalOptions = LayoutOptions.End;
            _bubble.StrokeShape = new RoundRectangle 
            { 
                CornerRadius = new CornerRadius(16, 16, 4, 16) 
            };
        }
        else
        {
            // Use mood color for character messages
            _bubble.BackgroundColor = MoodColor.WithAlpha(0.15f);
            _messageLabel.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Colors.White : Colors.Black;
            _senderLabel.TextColor = MoodColor;
            HorizontalOptions = LayoutOptions.Start;
            _bubble.StrokeShape = new RoundRectangle 
            { 
                CornerRadius = new CornerRadius(16, 16, 16, 4) 
            };
        }

        _senderLabel.Text = Sender;
        _senderLabel.IsVisible = !string.IsNullOrEmpty(Sender) && !IsFromUser;
    }

    private void StartTypingAnimation()
    {
        _typingCts?.Cancel();
        _typingCts = new CancellationTokenSource();
        
        _ = AnimateTypingDotsAsync(_typingCts.Token);
    }

    private void StopTypingAnimation()
    {
        _typingCts?.Cancel();
        _typingCts = null;
    }

    private async Task AnimateTypingDotsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            for (int i = 0; i < 3 && !ct.IsCancellationRequested; i++)
            {
                var dot = _typingDots[i];
                await dot.ScaleTo(1.3, 150, Easing.CubicOut);
                await dot.ScaleTo(1.0, 150, Easing.CubicIn);
                await Task.Delay(50, ct);
            }
            await Task.Delay(200, ct);
        }
    }
}

/// <summary>
/// Smooth progress indicator with pulse animation
/// </summary>
public class PulsingProgressRing : ContentView
{
    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(PulsingProgressRing), 
            false, propertyChanged: OnActiveChanged);

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(PulsingProgressRing), Colors.DodgerBlue);

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(PulsingProgressRing), 40.0);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private readonly Border _outerRing;
    private readonly Border _innerRing;
    private CancellationTokenSource? _animationCts;

    public PulsingProgressRing()
    {
        _outerRing = new Border
        {
            StrokeThickness = 3,
            BackgroundColor = Colors.Transparent,
            StrokeShape = new Ellipse()
        };

        _innerRing = new Border
        {
            StrokeThickness = 3,
            BackgroundColor = Colors.Transparent,
            StrokeShape = new Ellipse()
        };

        var grid = new Grid();
        grid.Children.Add(_outerRing);
        grid.Children.Add(_innerRing);
        
        Content = grid;
        IsVisible = false;
        UpdateSize();
    }

    private static void OnActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PulsingProgressRing ring)
        {
            var isActive = (bool)newValue;
            ring.IsVisible = isActive;
            
            if (isActive)
            {
                ring.StartAnimation();
            }
            else
            {
                ring.StopAnimation();
            }
        }
    }

    private void UpdateSize()
    {
        WidthRequest = Size;
        HeightRequest = Size;
        _outerRing.WidthRequest = Size;
        _outerRing.HeightRequest = Size;
        _innerRing.WidthRequest = Size * 0.6;
        _innerRing.HeightRequest = Size * 0.6;
        _innerRing.HorizontalOptions = LayoutOptions.Center;
        _innerRing.VerticalOptions = LayoutOptions.Center;
    }

    private void StartAnimation()
    {
        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        
        _outerRing.Stroke = new SolidColorBrush(Color);
        _innerRing.Stroke = new SolidColorBrush(Color.WithAlpha(0.5f));
        
        _ = AnimateAsync(_animationCts.Token);
    }

    private void StopAnimation()
    {
        _animationCts?.Cancel();
        _animationCts = null;
    }

    private async Task AnimateAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Rotate and pulse
            var rotateOuter = _outerRing.RotateTo(360, 2000, Easing.Linear);
            var rotateInner = _innerRing.RotateTo(-360, 1500, Easing.Linear);
            var pulseOut = _innerRing.ScaleTo(1.2, 500, Easing.CubicOut);
            
            await Task.WhenAll(pulseOut);
            await _innerRing.ScaleTo(1.0, 500, Easing.CubicIn);
            
            _outerRing.Rotation = 0;
            _innerRing.Rotation = 0;
        }
    }
}

/// <summary>
/// Animated card with hover/tap effects and smooth transitions
/// </summary>
public class AnimatedCard : Border
{
    public static readonly BindableProperty IsElevatedProperty =
        BindableProperty.Create(nameof(IsElevated), typeof(bool), typeof(AnimatedCard), true);

    public static readonly BindableProperty HighlightColorProperty =
        BindableProperty.Create(nameof(HighlightColor), typeof(Color), typeof(AnimatedCard), Colors.DodgerBlue);

    public bool IsElevated
    {
        get => (bool)GetValue(IsElevatedProperty);
        set => SetValue(IsElevatedProperty, value);
    }

    public Color HighlightColor
    {
        get => (Color)GetValue(HighlightColorProperty);
        set => SetValue(HighlightColorProperty, value);
    }

    private bool _isPressed;

    public AnimatedCard()
    {
        StrokeThickness = 0;
        Padding = new Thickness(16);
        BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#1E1E1E")
            : Colors.White;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) };
        
        // Add shadow for elevation
        Shadow = IsElevated ? new Shadow
        {
            Brush = new SolidColorBrush(Colors.Black),
            Offset = new Point(0, 4),
            Radius = 8,
            Opacity = 0.15f
        } : null;

        // Set up gesture recognizers
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnTapped;
        GestureRecognizers.Add(tapGesture);

        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += OnPointerEntered;
        pointerGesture.PointerExited += OnPointerExited;
        pointerGesture.PointerPressed += OnPointerPressed;
        pointerGesture.PointerReleased += OnPointerReleased;
        GestureRecognizers.Add(pointerGesture);
    }

    public event EventHandler? Clicked;

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        await AnimateTapAsync();
        Clicked?.Invoke(this, EventArgs.Empty);
    }

    private async void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!_isPressed)
        {
            await this.ScaleTo(1.02, 150, Easing.CubicOut);
            if (IsElevated && Shadow != null)
            {
                Shadow.Opacity = 0.25f;
            }
        }
    }

    private async void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isPressed)
        {
            await this.ScaleTo(1.0, 150, Easing.CubicIn);
            if (IsElevated && Shadow != null)
            {
                Shadow.Opacity = 0.15f;
            }
        }
    }

    private async void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        _isPressed = true;
        await this.ScaleTo(0.98, 100, Easing.CubicOut);
    }

    private async void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        _isPressed = false;
        await this.ScaleTo(1.0, 100, Easing.CubicIn);
    }

    private async Task AnimateTapAsync()
    {
        var originalBackground = BackgroundColor;
        BackgroundColor = HighlightColor.WithAlpha(0.1f);
        
        await Task.Delay(100);
        BackgroundColor = originalBackground;
    }
}

/// <summary>
/// Smooth fade-in/out view for content transitions
/// </summary>
public class FadeTransitionView : ContentView
{
    public static readonly BindableProperty TransitionDurationProperty =
        BindableProperty.Create(nameof(TransitionDuration), typeof(uint), typeof(FadeTransitionView), (uint)300);

    public uint TransitionDuration
    {
        get => (uint)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    private View? _currentContent;

    public async Task TransitionToAsync(View newContent)
    {
        if (_currentContent != null)
        {
            await _currentContent.FadeTo(0, TransitionDuration / 2, Easing.CubicIn);
        }

        Content = newContent;
        _currentContent = newContent;
        
        newContent.Opacity = 0;
        await newContent.FadeTo(1, TransitionDuration / 2, Easing.CubicOut);
    }
}

/// <summary>
/// Animated stat counter with smooth number transitions
/// </summary>
public class AnimatedCounter : ContentView
{
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double), typeof(AnimatedCounter), 
            0.0, propertyChanged: OnValueChanged);

    public static readonly BindableProperty FormatProperty =
        BindableProperty.Create(nameof(Format), typeof(string), typeof(AnimatedCounter), "N0");

    public static readonly BindableProperty PrefixProperty =
        BindableProperty.Create(nameof(Prefix), typeof(string), typeof(AnimatedCounter), "");

    public static readonly BindableProperty SuffixProperty =
        BindableProperty.Create(nameof(Suffix), typeof(string), typeof(AnimatedCounter), "");

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public string Prefix
    {
        get => (string)GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public string Suffix
    {
        get => (string)GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    private readonly Label _label;
    private double _displayedValue;
    private CancellationTokenSource? _animationCts;

    public AnimatedCounter()
    {
        _label = new Label
        {
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        Content = _label;
        UpdateDisplay();
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AnimatedCounter counter)
        {
            counter.AnimateToValue((double)newValue);
        }
    }

    private void AnimateToValue(double targetValue)
    {
        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        
        _ = AnimateValueAsync(targetValue, _animationCts.Token);
    }

    private async Task AnimateValueAsync(double target, CancellationToken ct)
    {
        var start = _displayedValue;
        var duration = 500;
        var startTime = DateTime.Now;
        
        while (!ct.IsCancellationRequested)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            var progress = Math.Min(1.0, elapsed / duration);
            
            // Ease out cubic
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            
            _displayedValue = start + (target - start) * easedProgress;
            UpdateDisplay();
            
            if (progress >= 1.0) break;
            
            await Task.Delay(16, ct); // ~60fps
        }
        
        _displayedValue = target;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _label.Text = $"{Prefix}{_displayedValue.ToString(Format)}{Suffix}";
    }
}

/// <summary>
/// Shimmer loading placeholder for skeleton screens
/// </summary>
public class ShimmerView : ContentView
{
    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(ShimmerView), 
            true, propertyChanged: OnActiveChanged);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private readonly BoxView _shimmer;
    private CancellationTokenSource? _animationCts;

    public ShimmerView()
    {
        _shimmer = new BoxView
        {
            Color = Colors.Gray.WithAlpha(0.2f),
            CornerRadius = 8
        };
        
        Content = _shimmer;
        
        if (IsActive)
        {
            StartAnimation();
        }
    }

    private static void OnActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ShimmerView shimmer)
        {
            if ((bool)newValue)
            {
                shimmer.StartAnimation();
            }
            else
            {
                shimmer.StopAnimation();
            }
        }
    }

    private void StartAnimation()
    {
        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        _ = AnimateShimmerAsync(_animationCts.Token);
    }

    private void StopAnimation()
    {
        _animationCts?.Cancel();
        _animationCts = null;
    }

    private async Task AnimateShimmerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await _shimmer.FadeTo(0.4, 800, Easing.SinInOut);
            await _shimmer.FadeTo(0.15, 800, Easing.SinInOut);
        }
    }
}
