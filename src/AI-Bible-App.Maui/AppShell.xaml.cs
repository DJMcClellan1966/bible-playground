using AI_Bible_App.Maui.Views;

namespace AI_Bible_App.Maui;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Core production routes
		Routing.RegisterRoute("chat", typeof(ChatPage));
		Routing.RegisterRoute("prayer", typeof(PrayerPage));
		Routing.RegisterRoute("userselection", typeof(UserSelectionPage));
		
		// ═══════════════════════════════════════════════════════════════════
		// EXPERIMENTAL FEATURE ROUTES
		// These are accessible via the Experimental Labs page
		// ═══════════════════════════════════════════════════════════════════
		
		// Multi-character experiences (BETA)
		Routing.RegisterRoute("roundtable", typeof(RoundtableChatPage));
		Routing.RegisterRoute("wisdomcouncil", typeof(WisdomCouncilPage));
		Routing.RegisterRoute("prayerchain", typeof(PrayerChainPage));
		Routing.RegisterRoute("MultiCharacterSelectionPage", typeof(MultiCharacterSelectionPage));
		
		// AI Learning & Evolution (ALPHA)
		Routing.RegisterRoute("evolution", typeof(CharacterEvolutionPage));
		
		// Developer Tools (DEV)
		Routing.RegisterRoute("diagnostics", typeof(SystemDiagnosticsPage));
		Routing.RegisterRoute("offlinemodels", typeof(OfflineModelsPage));
		
		// Labs hub
		Routing.RegisterRoute("labs", typeof(ExperimentalLabsPage));
	}
}
