using AI_Bible_App.Maui.ViewModels;

namespace AI_Bible_App.Maui.Views;

public partial class CustomCharacterPage : ContentPage
{
    public CustomCharacterPage(CustomCharacterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CustomCharacterViewModel vm)
        {
            await vm.LoadCharactersAsync();
        }
    }
}
