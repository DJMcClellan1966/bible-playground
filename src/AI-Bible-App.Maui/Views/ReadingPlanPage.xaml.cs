namespace AI_Bible_App.Maui.Views;

public partial class ReadingPlanPage : ContentPage
{
    private readonly ViewModels.ReadingPlanViewModel _viewModel;

    public ReadingPlanPage(ViewModels.ReadingPlanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
