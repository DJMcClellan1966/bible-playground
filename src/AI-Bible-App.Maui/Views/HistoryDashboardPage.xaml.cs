using AI_Bible_App.Maui.ViewModels;

namespace AI_Bible_App.Maui.Views;

public partial class HistoryDashboardPage : ContentPage
{
    private readonly HistoryDashboardViewModel _viewModel;
    private string _activeTab = "chats";
    
    public HistoryDashboardPage(HistoryDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
    
    private void OnChatsTabClicked(object sender, EventArgs e)
    {
        if (_activeTab == "chats") return;
        _activeTab = "chats";
        UpdateTabStyles();
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
    
    private void OnPrayersTabClicked(object sender, EventArgs e)
    {
        if (_activeTab == "prayers") return;
        _activeTab = "prayers";
        UpdateTabStyles();
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
    
    private void OnReflectionsTabClicked(object sender, EventArgs e)
    {
        if (_activeTab == "reflections") return;
        _activeTab = "reflections";
        UpdateTabStyles();
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
    
    private void UpdateTabStyles()
    {
        var primary = (Color)Application.Current!.Resources["Primary"];
        var gray300 = (Color)Application.Current!.Resources["Gray300"];
        var gray800 = (Color)Application.Current!.Resources["Gray800"];
        
        // Reset all tabs
        ChatsTab.BackgroundColor = gray300;
        ChatsTab.TextColor = gray800;
        PrayersTab.BackgroundColor = gray300;
        PrayersTab.TextColor = gray800;
        ReflectionsTab.BackgroundColor = gray300;
        ReflectionsTab.TextColor = gray800;
        
        ChatsCollection.IsVisible = false;
        PrayersCollection.IsVisible = false;
        ReflectionsCollection.IsVisible = false;
        
        // Activate selected tab
        switch (_activeTab)
        {
            case "chats":
                ChatsTab.BackgroundColor = primary;
                ChatsTab.TextColor = Colors.White;
                ChatsCollection.IsVisible = true;
                break;
            case "prayers":
                PrayersTab.BackgroundColor = primary;
                PrayersTab.TextColor = Colors.White;
                PrayersCollection.IsVisible = true;
                break;
            case "reflections":
                ReflectionsTab.BackgroundColor = primary;
                ReflectionsTab.TextColor = Colors.White;
                ReflectionsCollection.IsVisible = true;
                break;
        }
    }
    
    private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatSessionSummary session)
        {
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
            
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            
            if (_viewModel.ViewChatCommand.CanExecute(session))
            {
                await _viewModel.ViewChatCommand.ExecuteAsync(session);
            }
        }
    }
    
    private async void OnPrayerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PrayerSummary prayer)
        {
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
            
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            
            if (_viewModel.ViewPrayerCommand.CanExecute(prayer))
            {
                await _viewModel.ViewPrayerCommand.ExecuteAsync(prayer);
            }
        }
    }
    
    private async void OnReflectionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ReflectionSummary reflection)
        {
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
            
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            
            if (_viewModel.ViewReflectionCommand.CanExecute(reflection))
            {
                await _viewModel.ViewReflectionCommand.ExecuteAsync(reflection);
            }
        }
    }
    
    private async void OnDeleteChatSwipe(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipeItem && swipeItem.BindingContext is ChatSessionSummary session)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            if (_viewModel.DeleteChatCommand.CanExecute(session))
            {
                await _viewModel.DeleteChatCommand.ExecuteAsync(session);
            }
        }
    }
    
    private async void OnDeletePrayerSwipe(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipeItem && swipeItem.BindingContext is PrayerSummary prayer)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            if (_viewModel.DeletePrayerCommand.CanExecute(prayer))
            {
                await _viewModel.DeletePrayerCommand.ExecuteAsync(prayer);
            }
        }
    }
    
    private async void OnDeleteReflectionSwipe(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipeItem && swipeItem.BindingContext is ReflectionSummary reflection)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            if (_viewModel.DeleteReflectionCommand.CanExecute(reflection))
            {
                await _viewModel.DeleteReflectionCommand.ExecuteAsync(reflection);
            }
        }
    }
}
