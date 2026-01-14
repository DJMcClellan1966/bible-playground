namespace AI_Bible_App.Maui.Views;

public partial class EmailSignInPage : ContentPage
{
    public EmailSignInPage(ViewModels.EmailSignInViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
