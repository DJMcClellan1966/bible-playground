using AI_Bible_App.Maui.ViewModels;
using System.Collections.Specialized;

namespace AI_Bible_App.Maui.Views;

public partial class RoundtableChatPage : ContentPage
{
    private RoundtableChatViewModel? _viewModel;

    public RoundtableChatPage(RoundtableChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        
        // Subscribe to Messages collection changes for auto-scroll
        if (viewModel.Messages is INotifyCollectionChanged notifyCollection)
        {
            notifyCollection.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel != null)
        {
            await _viewModel.InitializeAsync();
        }
    }

    private void OnContrarianCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox cb && cb.BindingContext is AI_Bible_App.Core.Models.BiblicalCharacter character)
        {
            _viewModel?.ToggleContrarianCommand.Execute(character);
        }
    }

    private async void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // Small delay to let UI update
            await Task.Delay(100);
            
            // Auto-scroll to bottom when new messages arrive
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await MessagesScrollView.ScrollToAsync(0, MessagesScrollView.ContentSize.Height, true);
            });
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Unsubscribe from collection changes
        if (_viewModel?.Messages is INotifyCollectionChanged notifyCollection)
        {
            notifyCollection.CollectionChanged -= OnMessagesCollectionChanged;
        }
    }
}
