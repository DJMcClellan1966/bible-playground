using AI_Bible_App.Maui.Views;
using AI_Bible_App.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AI_Bible_App.Maui;

public partial class AppShell : Shell
{
	private readonly IKeyboardShortcutService? _keyboardService;

	public AppShell()
	{
		InitializeComponent();
		
		// Get keyboard service and register shortcuts
		_keyboardService = Application.Current?.Handler?.MauiContext?.Services.GetService<IKeyboardShortcutService>();
		_keyboardService?.RegisterDefaultShortcuts();

		// Core production routes
		Routing.RegisterRoute("login", typeof(HallowLoginPage));
		Routing.RegisterRoute("emailsignin", typeof(EmailSignInPage));
		Routing.RegisterRoute("chat", typeof(ChatPage));
		Routing.RegisterRoute("modernchat", typeof(ModernChatPage));
		Routing.RegisterRoute("prayer", typeof(PrayerPage));
		Routing.RegisterRoute("devotional", typeof(DevotionalPage));
		Routing.RegisterRoute("bookmarks", typeof(BookmarksPage));
		Routing.RegisterRoute("customcharacters", typeof(CustomCharacterPage));
		Routing.RegisterRoute("readingplan", typeof(ReadingPlanPage));
		Routing.RegisterRoute("SubscriptionPage", typeof(SubscriptionPage));
		
		// New UI Enhancement Routes
		Routing.RegisterRoute("BibleReader", typeof(BibleReaderPage));
		Routing.RegisterRoute("HistoryDashboard", typeof(HistoryDashboardPage));
		Routing.RegisterRoute("CreateCharacter", typeof(CustomCharacterPage));
		
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
	
	/// <summary>
	/// Handle keyboard shortcuts from platform-specific handlers
	/// </summary>
	public bool HandleKeyboardShortcut(string key, bool ctrl, bool shift, bool alt)
	{
		return _keyboardService?.HandleKeyPress(key, ctrl, shift, alt) ?? false;
	}
}
