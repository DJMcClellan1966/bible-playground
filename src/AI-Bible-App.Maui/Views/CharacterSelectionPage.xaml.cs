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
        try
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OnCharacterSelected fired");
            
            var character = e.CurrentSelection.FirstOrDefault() as BiblicalCharacter;
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Character: {character?.Name ?? "null"}");
            
            // DO NOT clear selection here - causes WinUI3 crash
            // Will clear after command execution completes
            
            if (character != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Dispatching command for {character.Name}");
                
                // Capture sender for later selection clearing
                var collectionView = sender as CollectionView;
                
                Dispatcher.Dispatch(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Inside Dispatcher.Dispatch");
                        
                        if (_viewModel.SelectCharacterCommand.CanExecute(character))
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Executing command");
                            await _viewModel.SelectCharacterCommand.ExecuteAsync(character);
                            
                            // Clear selection AFTER command completes
                            if (collectionView != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] Clearing selection after command");
                                collectionView.SelectedItem = null;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Command cannot execute");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Command execution failed: {ex}");
                        await DisplayAlert("Error", $"Failed to select character: {ex.Message}", "OK");
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Character is null, skipping");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] OnCharacterSelected crashed: {ex}");
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
    
    private async void OnCharacterCardTapped(object sender, TappedEventArgs e)
    {
        if (CharacterCarousel.CurrentItem is BiblicalCharacter character)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            try
            {
                if (_viewModel.SelectCharacterCommand.CanExecute(character))
                {
                    await _viewModel.SelectCharacterCommand.ExecuteAsync(character);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] OnCharacterCardTapped failed: {ex}");
                await DisplayAlert("Error", $"Failed to select character: {ex.Message}", "OK");
            }
        }
    }
    
    private void OnCardsViewClicked(object sender, EventArgs e)
    {
        if (_isCarouselView) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine("[DEBUG] Switching to cards view");
            
            _isCarouselView = true;
            
            Dispatcher.Dispatch(() =>
            {
                try
                {
                    CarouselContainer.IsVisible = true;
                    ListContainer.IsVisible = false;
                    
                    // Update button styles
                    if (Application.Current?.Resources != null)
                    {
                        CardsViewButton.BackgroundColor = (Color)Application.Current.Resources["Primary"];
                        CardsViewButton.TextColor = Colors.White;
                        ListViewButton.BackgroundColor = (Color)Application.Current.Resources["Gray300"];
                        ListViewButton.TextColor = (Color)Application.Current.Resources["Gray800"];
                    }
                    
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Cards view switch completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Cards view switch inner failed: {ex}");
                }
            });
            
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] OnCardsViewClicked failed: {ex}");
        }
    }
    
    private void OnListViewClicked(object sender, EventArgs e)
    {
        if (!_isCarouselView) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine("[DEBUG] Switching to list view");
            
            _isCarouselView = false;
            
            Dispatcher.Dispatch(() =>
            {
                try
                {
                    CarouselContainer.IsVisible = false;
                    ListContainer.IsVisible = true;
                    
                    // Update button styles
                    if (Application.Current?.Resources != null)
                    {
                        ListViewButton.BackgroundColor = (Color)Application.Current.Resources["Primary"];
                        ListViewButton.TextColor = Colors.White;
                        CardsViewButton.BackgroundColor = (Color)Application.Current.Resources["Gray300"];
                        CardsViewButton.TextColor = (Color)Application.Current.Resources["Gray800"];
                    }
                    
                    System.Diagnostics.Debug.WriteLine("[DEBUG] List view switch completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] List view switch inner failed: {ex}");
                }
            });
            
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] OnListViewClicked failed: {ex}");
        }
    }
    
    private async void OnSwipeChatInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipeItem && swipeItem.BindingContext is BiblicalCharacter character)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            
            try
            {
                if (_viewModel.SelectCharacterCommand.CanExecute(character))
                {
                    await _viewModel.SelectCharacterCommand.ExecuteAsync(character);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] OnSwipeChatInvoked failed: {ex}");
                await DisplayAlert("Error", $"Failed to select character: {ex.Message}", "OK");
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
