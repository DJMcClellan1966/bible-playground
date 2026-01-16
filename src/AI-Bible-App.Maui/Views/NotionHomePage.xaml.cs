namespace AI_Bible_App.Maui.Views;

public partial class NotionHomePage : ContentPage
{
    public NotionHomePage(ViewModels.HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is ViewModels.HomeViewModel vm)
        {
            await vm.LoadDataAsync();
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        if (BindingContext is ViewModels.HomeViewModel vm && vm.OpenSettingsCommand.CanExecute(null))
        {
            await vm.OpenSettingsCommand.ExecuteAsync(null);
        }
    }

    private async void OnNewChatTapped(object sender, EventArgs e)
    {
        if (BindingContext is ViewModels.HomeViewModel vm && vm.NewChatCommand.CanExecute(null))
        {
            await vm.NewChatCommand.ExecuteAsync(null);
        }
    }

    private async void OnPrayerTapped(object sender, EventArgs e)
    {
        if (BindingContext is ViewModels.HomeViewModel vm && vm.PrayerCommand.CanExecute(null))
        {
            await vm.PrayerCommand.ExecuteAsync(null);
        }
    }

    private async void OnBibleTapped(object sender, EventArgs e)
    {
        if (BindingContext is ViewModels.HomeViewModel vm && vm.BibleCommand.CanExecute(null))
        {
            await vm.BibleCommand.ExecuteAsync(null);
        }
    }
}
