using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Repositories;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using AI_Bible_App.Maui.Services.Core;
using AI_Bible_App.Maui.ViewModels;
using AI_Bible_App.Maui.Views;
using CommunityToolkit.Maui;
using System.Reflection;

namespace AI_Bible_App.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Add Configuration
		var assembly = Assembly.GetExecutingAssembly();
		using var stream = assembly.GetManifestResourceStream("AI_Bible_App.Maui.appsettings.json");
		if (stream != null)
		{
			var config = new ConfigurationBuilder()
				.AddJsonStream(stream)
				.Build();
			builder.Configuration.AddConfiguration(config);
		}
		builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

		// Core services
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		builder.Services.AddSingleton<IDialogService, DialogService>();
		builder.Services.AddSingleton<ICharacterRepository, InMemoryCharacterRepository>();
		builder.Services.AddSingleton<IChatRepository, JsonChatRepository>();
		builder.Services.AddSingleton<IPrayerRepository, JsonPrayerRepository>();
		builder.Services.AddSingleton<IReflectionRepository, JsonReflectionRepository>();
		builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();
		builder.Services.AddSingleton<IBibleLookupService, BibleLookupService>();
		builder.Services.AddSingleton<ITrainingDataExporter, TrainingDataExporter>();
		
		// New feature services
		builder.Services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
		builder.Services.AddSingleton<IDevotionalRepository, DevotionalRepository>();
		builder.Services.AddSingleton<IVerseBookmarkRepository, VerseBookmarkRepository>();
		builder.Services.AddSingleton<IBibleVerseIndexService, BibleVerseIndexService>();
		builder.Services.AddSingleton<ISecureConfigService, SecureConfigService>();
		
		// Offline AI services
		builder.Services.AddSingleton<AI_Bible_App.Core.Services.IConnectivityService, ConnectivityService>();
		builder.Services.AddSingleton<AI_Bible_App.Core.Services.IOfflineAIService, OfflineAIService>();
		
		// Model warmup service for faster first responses
		builder.Services.AddSingleton<IModelWarmupService, ModelWarmupService>();
		
		// Text-to-speech for character voices
		// Use platform-specific implementation on Windows for better compatibility
#if WINDOWS
		builder.Services.AddSingleton<ICharacterVoiceService, AI_Bible_App.Maui.Platforms.Windows.Services.WindowsSpeechService>();
#else
		builder.Services.AddSingleton<Microsoft.Maui.Media.ITextToSpeech>(sp => TextToSpeech.Default);
		builder.Services.AddSingleton<ICharacterVoiceService, CharacterVoiceService>();
#endif
		
		// User management and content moderation
		builder.Services.AddSingleton<IUserRepository, JsonUserRepository>();
		builder.Services.AddSingleton<IUserService, UserService>();
		builder.Services.AddSingleton<IContentModerationService, ContentModerationService>();
		
		// Character Intelligence Service - evolving character personalities
		builder.Services.AddSingleton<CharacterIntelligenceService>();
		
		// Cross-Character Learning Service - enables characters to learn from roundtable discussions
		builder.Services.AddSingleton<ICrossCharacterLearningService, CrossCharacterLearningService>();
		
		// ═══════════════════════════════════════════════════════════════════════════════
		// CORE OPTIMIZATION SERVICES - High-performance, interconnected systems
		// ═══════════════════════════════════════════════════════════════════════════════
		
		// Intelligent caching with semantic similarity matching
		builder.Services.AddSingleton<IIntelligentCacheService, IntelligentCacheService>();
		
		// Character mood system - dynamic emotional states for authentic interactions
		builder.Services.AddSingleton<ICharacterMoodService, CharacterMoodService>();
		
		// Scripture context engine - intelligent Bible verse finder
		builder.Services.AddSingleton<IScriptureContextEngine, ScriptureContextEngine>();
		
		// Performance monitoring - real-time metrics and health checks
		builder.Services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
		
		// Conversation flow predictor - anticipates user questions
		builder.Services.AddSingleton<IConversationFlowPredictor, ConversationFlowPredictor>();
		
		// Core services orchestrator - coordinates all systems for optimal performance
		builder.Services.AddSingleton<ICoreServicesOrchestrator, CoreServicesOrchestrator>();
		
		// Chat enhancement service - integrates core systems for chat interactions
		builder.Services.AddSingleton<IChatEnhancementService, ChatEnhancementService>();
		
		// Multi-Character Chat Service
		builder.Services.AddSingleton<IMultiCharacterChatService, MultiCharacterChatService>();
		
		// Image Generation Service - AI-generated character portraits and scenes
		builder.Services.AddSingleton<IImageGenerationService, ImageGenerationService>();
		
		// Device capability detection for tiered AI
		builder.Services.AddSingleton<IDeviceCapabilityService>(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<DeviceCapabilityService>>();
			// Inject MAUI connectivity check
			Func<bool> networkCheck = () => 
			{
				try { return Connectivity.Current.NetworkAccess == NetworkAccess.Internet; }
				catch { return true; }
			};
			return new DeviceCapabilityService(logger, networkCheck);
		});
		
		// AI Services - Tiered system with fallback chain
		// Tier 1: Local Ollama (desktop) / On-device LLamaSharp (capable mobile)
		// Tier 2: Cloud API (Groq) - best quality when online  
		// Tier 3: Cached responses - emergency fallback for limited devices
		builder.Services.AddSingleton<LocalAIService>();
		builder.Services.AddSingleton<GroqAIService>();
		builder.Services.AddSingleton<CachedResponseAIService>();
		
		// On-device AI is only used on mobile, registered as null on desktop
		// The HybridAIService accepts OnDeviceAIService? and handles null gracefully
#pragma warning disable CS8634 // Nullable type doesn't match class constraint
		builder.Services.AddSingleton<OnDeviceAIService?>(sp => (OnDeviceAIService?)null);
#pragma warning restore CS8634
		
		builder.Services.AddSingleton<IAIService, HybridAIService>();

		// Register ViewModels
		builder.Services.AddTransient<UserSelectionViewModel>();
		builder.Services.AddTransient<CharacterSelectionViewModel>();
		builder.Services.AddTransient<ChatViewModel>();
		builder.Services.AddTransient<ChatHistoryViewModel>();
		builder.Services.AddTransient<PrayerViewModel>();
		builder.Services.AddTransient<ReflectionViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<OfflineModelsViewModel>();
		builder.Services.AddTransient<MultiCharacterSelectionViewModel>();
		builder.Services.AddTransient<WisdomCouncilViewModel>();
		builder.Services.AddTransient<PrayerChainViewModel>();
		builder.Services.AddTransient<RoundtableChatViewModel>();
		builder.Services.AddTransient<CharacterEvolutionViewModel>();
		builder.Services.AddTransient<SystemDiagnosticsViewModel>();

		// Register Pages
		builder.Services.AddTransient<UserSelectionPage>();
		builder.Services.AddTransient<CharacterSelectionPage>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddTransient<ChatHistoryPage>();
		builder.Services.AddTransient<PrayerPage>();
		builder.Services.AddTransient<ReflectionPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<OfflineModelsPage>();
		builder.Services.AddTransient<MultiCharacterSelectionPage>();
		builder.Services.AddTransient<WisdomCouncilPage>();
		builder.Services.AddTransient<PrayerChainPage>();
		builder.Services.AddTransient<RoundtableChatPage>();
		builder.Services.AddTransient<CharacterEvolutionPage>();
		builder.Services.AddTransient<SystemDiagnosticsPage>();

		return builder.Build();
	}
}
