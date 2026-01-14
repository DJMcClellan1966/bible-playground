namespace AI_Bible_App.Maui.Views;

public partial class ModernChatPage : ContentPage
{
    public ModernChatPage(ViewModels.ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        
        // Auto-scroll to bottom when messages are added
        viewModel.Messages.CollectionChanged += async (s, e) =>
        {
            await Task.Delay(100);
            await ChatScrollView.ScrollToAsync(0, ChatScrollView.ContentSize.Height, true);
        };
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        MessageEntry.Focus();
    }
}
