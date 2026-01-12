namespace AI_Bible_App.Maui.Views;

public partial class AdminPage : ContentPage
{
    public AdminPage(AdminViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is AdminViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
