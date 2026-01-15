using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.ViewModels;

namespace AI_Bible_App.Maui.Views;

public partial class BibleReaderPage : ContentPage
{
    private readonly BibleReaderViewModel _viewModel;
    
    public BibleReaderPage(BibleReaderViewModel viewModel)
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
    
    private void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is BibleVerseSearchResult result)
        {
            if (result.IsPlaceholder || string.Equals(result.Reference, "No results", StringComparison.OrdinalIgnoreCase))
                return;

            // Clear selection
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
            
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            
            // Navigate to the verse
            if (_viewModel.GoToVerseCommand.CanExecute(result.Reference))
            {
                _viewModel.GoToVerseCommand.Execute(result.Reference);
            }
        }
    }
    
    private void OnVerseSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is BibleVerse verse)
        {
            // Clear selection
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
            
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            
            // Show verse actions
            if (_viewModel.ShowVerseActionsCommand.CanExecute(verse))
            {
                _viewModel.ShowVerseActionsCommand.Execute(verse);
            }
        }
    }
    
    private async void OnBookmarkSwipe(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipeItem && swipeItem.BindingContext is BibleVerseSearchResult result)
        {
            if (result.IsPlaceholder || string.Equals(result.Reference, "No results", StringComparison.OrdinalIgnoreCase))
                return;

            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            if (_viewModel.BookmarkVerseCommand.CanExecute(result.Reference))
            {
                await _viewModel.BookmarkVerseCommand.ExecuteAsync(result.Reference);
            }
        }
    }
}
