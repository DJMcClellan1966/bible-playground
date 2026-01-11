using Microsoft.Maui.Controls;

namespace AI_Bible_App.Maui.Views;

public partial class ExperimentalLabsPage : ContentPage
{
    public ExperimentalLabsPage()
    {
        InitializeComponent();
    }

    private async void OnRoundtableClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("roundtable");
    }

    private async void OnWisdomCouncilClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("wisdomcouncil");
    }

    private async void OnPrayerChainClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("prayerchain");
    }

    private async void OnCharacterEvolutionClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("evolution");
    }

    private async void OnDiagnosticsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("diagnostics");
    }

    private async void OnOfflineModelsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("offlinemodels");
    }
}
