using System.Collections.ObjectModel;

namespace AI_Bible_App.Maui.Controls;

/// <summary>
/// Beautiful roundtable discussion visualization
/// Shows participants arranged around a virtual table with speaking animations
/// </summary>
public partial class EnhancedRoundtableView : ContentView
{
    #region Bindable Properties
    
    public static readonly BindableProperty TopicProperty = BindableProperty.Create(
        nameof(Topic),
        typeof(string),
        typeof(EnhancedRoundtableView),
        "Discussion Topic",
        propertyChanged: OnTopicChanged);
    
    public static readonly BindableProperty ParticipantsProperty = BindableProperty.Create(
        nameof(Participants),
        typeof(ObservableCollection<string>),
        typeof(EnhancedRoundtableView),
        null,
        propertyChanged: OnParticipantsChanged);
    
    public static readonly BindableProperty CurrentSpeakerProperty = BindableProperty.Create(
        nameof(CurrentSpeaker),
        typeof(string),
        typeof(EnhancedRoundtableView),
        null,
        propertyChanged: OnCurrentSpeakerChanged);
    
    public static readonly BindableProperty CurrentSpeechProperty = BindableProperty.Create(
        nameof(CurrentSpeech),
        typeof(string),
        typeof(EnhancedRoundtableView),
        null,
        propertyChanged: OnCurrentSpeechChanged);
    
    public string Topic
    {
        get => (string)GetValue(TopicProperty);
        set => SetValue(TopicProperty, value);
    }
    
    public ObservableCollection<string> Participants
    {
        get => (ObservableCollection<string>)GetValue(ParticipantsProperty);
        set => SetValue(ParticipantsProperty, value);
    }
    
    public string? CurrentSpeaker
    {
        get => (string?)GetValue(CurrentSpeakerProperty);
        set => SetValue(CurrentSpeakerProperty, value);
    }
    
    public string? CurrentSpeech
    {
        get => (string?)GetValue(CurrentSpeechProperty);
        set => SetValue(CurrentSpeechProperty, value);
    }
    
    #endregion
    
    private readonly Dictionary<string, CharacterAvatar> _participantAvatars = new();
    
    public EnhancedRoundtableView()
    {
        InitializeComponent();
    }
    
    private static void OnTopicChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EnhancedRoundtableView view && newValue is string topic)
        {
            view.TopicLabel.Text = topic;
        }
    }
    
    private static void OnParticipantsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EnhancedRoundtableView view)
        {
            view.UpdateParticipantsLayout();
        }
    }
    
    private static void OnCurrentSpeakerChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EnhancedRoundtableView view)
        {
            view.UpdateSpeakingState((string?)oldValue, (string?)newValue);
        }
    }
    
    private static void OnCurrentSpeechChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EnhancedRoundtableView view && newValue is string speech)
        {
            view.SpeechLabel.Text = speech;
            
            if (!string.IsNullOrEmpty(speech))
            {
                view.SpeakerPanel.IsVisible = true;
                _ = view.AnimateSpeechIn();
            }
        }
    }
    
    private void UpdateParticipantsLayout()
    {
        ParticipantsGrid.Children.Clear();
        _participantAvatars.Clear();
        
        if (Participants == null || !Participants.Any())
            return;
        
        var positions = GetCirclePositions(Participants.Count);
        
        for (int i = 0; i < Participants.Count; i++)
        {
            var name = Participants[i];
            var position = positions[i];
            
            var avatar = CreateParticipantAvatar(name);
            
            // Position avatar
            avatar.HorizontalOptions = LayoutOptions.Start;
            avatar.VerticalOptions = LayoutOptions.Start;
            avatar.Margin = new Thickness(position.X, position.Y, 0, 0);
            
            ParticipantsGrid.Children.Add(avatar);
            _participantAvatars[name] = avatar;
            
            // Animate entrance
            _ = AnimateParticipantEntrance(avatar, i * 100);
        }
    }
    
    private CharacterAvatar CreateParticipantAvatar(string characterName)
    {
        var avatar = new CharacterAvatar
        {
            CharacterName = characterName,
            Size = 56,
            IsSpeaking = false
        };
        
        // Add name label below
        var container = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center
        };
        
        var nameLabel = new Label
        {
            Text = GetShortName(characterName),
            FontSize = 10,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };
        
        // Note: For simplicity, we're returning just the avatar
        // In a full implementation, you'd wrap this in a container
        
        return avatar;
    }
    
    private List<Point> GetCirclePositions(int count)
    {
        var positions = new List<Point>();
        var centerX = 170; // Half of grid width minus avatar size
        var centerY = 130; // Half of grid height minus avatar size
        var radiusX = 150;
        var radiusY = 100;
        
        // Start from top and go clockwise
        var startAngle = -Math.PI / 2;
        
        for (int i = 0; i < count; i++)
        {
            var angle = startAngle + (2 * Math.PI * i / count);
            var x = centerX + radiusX * Math.Cos(angle);
            var y = centerY + radiusY * Math.Sin(angle);
            positions.Add(new Point(x, y));
        }
        
        return positions;
    }
    
    private async Task AnimateParticipantEntrance(VisualElement element, int delayMs)
    {
        element.Opacity = 0;
        element.Scale = 0.5;
        
        await Task.Delay(delayMs);
        
        await Task.WhenAll(
            element.FadeTo(1, 300, Easing.CubicOut),
            element.ScaleTo(1, 300, Easing.SpringOut)
        );
    }
    
    private void UpdateSpeakingState(string? oldSpeaker, string? newSpeaker)
    {
        // Stop old speaker animation
        if (!string.IsNullOrEmpty(oldSpeaker) && _participantAvatars.TryGetValue(oldSpeaker, out var oldAvatar))
        {
            oldAvatar.IsSpeaking = false;
        }
        
        // Start new speaker animation
        if (!string.IsNullOrEmpty(newSpeaker))
        {
            if (_participantAvatars.TryGetValue(newSpeaker, out var newAvatar))
            {
                newAvatar.IsSpeaking = true;
            }
            
            // Update speaker panel
            SpeakerAvatar.CharacterName = newSpeaker;
            SpeakerNameLabel.Text = newSpeaker;
            SpeakerPanel.IsVisible = true;
        }
        else
        {
            SpeakerPanel.IsVisible = false;
        }
    }
    
    private async Task AnimateSpeechIn()
    {
        SpeakerPanel.TranslationY = 20;
        SpeakerPanel.Opacity = 0.5;
        
        await Task.WhenAll(
            SpeakerPanel.TranslateTo(0, 0, 200, Easing.CubicOut),
            SpeakerPanel.FadeTo(1, 200, Easing.CubicOut)
        );
    }
    
    private string GetShortName(string fullName)
    {
        // Get first name or abbreviation
        var parts = fullName.Split(' ');
        if (parts.Length > 0)
        {
            var name = parts[0];
            if (name.Length > 8)
                return name.Substring(0, 7) + "...";
            return name;
        }
        return fullName;
    }
    
    /// <summary>
    /// Show a message being typed out character by character
    /// </summary>
    public async Task ShowMessageWithTypewriterEffect(string speaker, string message)
    {
        CurrentSpeaker = speaker;
        SpeechLabel.Text = "";
        SpeakerPanel.IsVisible = true;
        
        foreach (char c in message)
        {
            SpeechLabel.Text += c;
            await Task.Delay(15); // Typing speed
        }
    }
    
    /// <summary>
    /// Highlight the table center for emphasis
    /// </summary>
    public async Task PulseTable()
    {
        await TableShape.ScaleTo(1.05, 200, Easing.CubicOut);
        await TableShape.ScaleTo(1.0, 200, Easing.CubicIn);
    }
    
    /// <summary>
    /// Clear the current speech
    /// </summary>
    public void ClearSpeech()
    {
        CurrentSpeaker = null;
        CurrentSpeech = null;
        SpeakerPanel.IsVisible = false;
    }
}
