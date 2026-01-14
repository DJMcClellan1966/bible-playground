namespace AI_Bible_App.Maui.Views;

public partial class HallowLoginPage : ContentPage
{
    public HallowLoginPage(ViewModels.HallowLoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is ViewModels.HallowLoginViewModel vm)
        {
            await vm.CheckExistingSessionAsync();
        }
    }
}
