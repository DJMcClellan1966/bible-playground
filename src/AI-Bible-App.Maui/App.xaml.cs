using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Maui.Services;
using AI_Bible_App.Infrastructure.Services;
using System.Runtime.ExceptionServices;

namespace AI_Bible_App.Maui;

public partial class App : Application
{
	private readonly IUserService _userService;
	private readonly IModelWarmupService? _warmupService;
	private readonly IConfiguration? _configuration;
	private readonly IFontScaleService? _fontScaleService;
	private readonly IBibleVerseIndexService? _bibleIndexService;

	public App(IUserService userService, IModelWarmupService? warmupService = null, IConfiguration? configuration = null,
		IFontScaleService? fontScaleService = null, IBibleVerseIndexService? bibleIndexService = null)
	{
		InitializeComponent();
		_userService = userService;
		_warmupService = warmupService;
		_configuration = configuration;
		_fontScaleService = fontScaleService;
		_bibleIndexService = bibleIndexService;
		
		// Global exception handling
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			var ex = e.ExceptionObject as Exception;
			System.Diagnostics.Debug.WriteLine($"[CRASH] Unhandled exception: {ex?.Message}");
			System.Diagnostics.Debug.WriteLine($"[CRASH] Stack trace: {ex?.StackTrace}");
			LogCrashToFile(ex);
		};
		
		TaskScheduler.UnobservedTaskException += (s, e) =>
		{
			System.Diagnostics.Debug.WriteLine($"[CRASH] Unobserved task exception: {e.Exception.Message}");
			System.Diagnostics.Debug.WriteLine($"[CRASH] Stack trace: {e.Exception.StackTrace}");
			LogCrashToFile(e.Exception);
			e.SetObserved(); // Prevent crash
		};
	}
	
	private static void LogCrashToFile(Exception? ex)
	{
		try
		{
			var crashLog = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
			var message = $"[{DateTime.Now}] {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}\n\n";
			File.AppendAllText(crashLog, message);
		}
		catch { }
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shell = new AppShell();
		var window = new Window(shell);
		
		// Handle startup - check if we need user selection and warm up model
		window.Created += async (s, e) =>
		{
			try
			{
				// Pre-warm AI model in background if enabled (non-blocking)
				if (_warmupService != null && _configuration?["Ollama:PrewarmOnStartup"] != "false")
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await _warmupService.WarmupModelAsync();
						}
						catch (Exception warmupEx)
						{
							System.Diagnostics.Debug.WriteLine($"[App] Model warmup error (non-critical): {warmupEx.Message}");
						}
					});
				}
				
				// Initialize Bible verse index in background (non-blocking)
				if (_bibleIndexService != null)
				{
					_ = Task.Run(async () =>
					{
						try
						{
							// Load Bible verses from bundled data
							var bibleFiles = new[] { "web.json", "kjv.json", "asv.json" };
							foreach (var fileName in bibleFiles)
							{
								try
								{
									using var stream = await FileSystem.OpenAppPackageFileAsync($"Data/Bible/{fileName}");
									using var reader = new StreamReader(stream);
									var json = await reader.ReadToEndAsync();
									var verses = System.Text.Json.JsonSerializer.Deserialize<List<AI_Bible_App.Core.Models.BibleVerse>>(json);
									if (verses != null && verses.Count > 0)
									{
										await _bibleIndexService.InitializeWithVersesAsync(verses);
										System.Diagnostics.Debug.WriteLine($"[App] Bible index ready: {_bibleIndexService.TotalVersesIndexed} verses from {fileName}");
										break;
									}
								}
								catch (Exception fileEx)
								{
									System.Diagnostics.Debug.WriteLine($"[App] Could not load {fileName}: {fileEx.Message}");
								}
							}
						}
						catch (Exception indexEx)
						{
							System.Diagnostics.Debug.WriteLine($"[App] Bible index error (non-critical): {indexEx.Message}");
						}
					});
				}
				
				// Try auto-login with last active user
				var autoLoggedIn = await _userService.TryAutoLoginAsync();
				
				// Apply font scale from user settings if logged in
				if (autoLoggedIn && _userService.CurrentUser != null && _fontScaleService != null)
				{
					_fontScaleService.ApplyScale(_userService.CurrentUser.Settings.FontSizePreference);
				}
				
				if (!autoLoggedIn)
				{
					// No user found, show user selection page
					await Shell.Current.GoToAsync("//userselection");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[App] Startup error: {ex.Message}");
			}
		};
		
		return window;
	}
}