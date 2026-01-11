using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.ViewModels;

namespace AI_Bible_App.Maui.Views;

public partial class CharacterSelectionPage : ContentPage
{
    private readonly CharacterSelectionViewModel _viewModel;
    private bool _isCarouselView = true;
    
    public CharacterSelectionPage(CharacterSelectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        
        // Connect carousel to indicator
        CharacterIndicator.SetBinding(IndicatorView.ItemsSourceProperty, 
            new Binding(nameof(CharacterSelectionViewModel.Characters), source: viewModel));
        CharacterCarousel.IndicatorView = CharacterIndicator;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
    
    private void OnCharacterSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is BiblicalCharacter character)
        {
            // Clear selection to allow re-selection
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
                
            if (_viewModel.SelectCharacterCommand.CanExecute(character))
            {
                _viewModel.SelectCharacterCommand.Execute(character);
            }
        }
    }
    
    private void OnCarouselItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        // Provide haptic feedback on character change
        if (e.CurrentItem is BiblicalCharacter character)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
    }
    
    private void OnCharacterCardTapped(object sender, TappedEventArgs e)
    {
        if (CharacterCarousel.CurrentItem is BiblicalCharacter character)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            if (_viewModel.SelectCharacterCommand.CanExecute(character))
            {
                _viewModel.SelectCharacterCommand.Execute(character);
            }
        }
    }
    
    private void OnCardsViewClicked(object sender, EventArgs e)
    {
        if (_isCarouselView) return;
        
        _isCarouselView = true;
        CarouselContainer.IsVisible = true;
        ListContainer.IsVisible = false;
        
        // Update button styles
        CardsViewButton.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
        CardsViewButton.TextColor = Colors.White;
        ListViewButton.BackgroundColor = (Color)Application.Current!.Resources["Gray300"];
        ListViewButton.TextColor = (Color)Application.Current!.Resources["Gray800"];
        
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
    
    private void OnListViewClicked(object sender, EventArgs e)
    {
        if (!_isCarouselView) return;
        
        _isCarouselView = false;
        CarouselContainer.IsVisible = false;
        ListContainer.IsVisible = true;
        
        // Update button styles
        ListViewButton.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
        ListViewButton.TextColor = Colors.White;
        CardsViewButton.BackgroundColor = (Color)Application.Current!.Resources["Gray300"];
        CardsViewButton.TextColor = (Color)Application.Current!.Resources["Gray800"];
        
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }
    
    private void OnSwipeChatInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipeItem && swipeItem.BindingContext is BiblicalCharacter character)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            if (_viewModel.SelectCharacterCommand.CanExecute(character))
            {
                _viewModel.SelectCharacterCommand.Execute(character);
            }
        }
    }
    
    private async void OnSwipeInfoInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipeItem && swipeItem.BindingContext is BiblicalCharacter character)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            
            await DisplayAlert(
                character.Name,
                $"{character.Title}\n\n{character.Description}\n\nEra: {character.Era}",
                "Close");
        }
    }
    
    private async void OnBibleReaderClicked(object sender, EventArgs e)
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        await Shell.Current.GoToAsync("///BibleReader");
    }
    
    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        await Shell.Current.GoToAsync("///HistoryDashboard");
    }
    
    private async void OnCreateCharacterClicked(object sender, EventArgs e)
    {
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        await Shell.Current.GoToAsync("///CreateCharacter");
    }
}
