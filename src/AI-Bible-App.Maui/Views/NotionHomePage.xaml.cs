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
}
