using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using AI_Bible_App.Maui.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AI_Bible_App.Maui.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		base.OnLaunched(args);

		// Hook up keyboard shortcuts for the main window
		var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
		if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window winUIWindow)
		{
			winUIWindow.Content.KeyDown += OnKeyDown;
			System.Diagnostics.Debug.WriteLine("[Windows] Keyboard shortcuts wired up");
		}
	}

	private void OnKeyDown(object sender, KeyRoutedEventArgs e)
	{
		var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
			.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
		var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
			.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
		var alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
			.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

		// Get key name
		var key = e.Key.ToString();
		
		// Get keyboard service
		var shell = Microsoft.Maui.Controls.Shell.Current as AI_Bible_App.Maui.AppShell;
		if (shell != null && shell.HandleKeyboardShortcut(key, ctrl, shift, alt))
		{
			e.Handled = true;
		}
	}
}
