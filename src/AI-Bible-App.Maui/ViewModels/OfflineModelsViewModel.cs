using AI_Bible_App.Core.Services;
using AI_Bible_App.Infrastructure.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace AI_Bible_App.Maui.ViewModels;

public partial class OfflineModelsViewModel : BaseViewModel
{
    private readonly IOfflineAIService _offlineService;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<OfflineModelsViewModel> _logger;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private ObservableCollection<LocalModelItemViewModel> _models = new();

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private string _currentModelName = "";

    [ObservableProperty]
    private bool _isLoading;

    public OfflineModelsViewModel(
        IOfflineAIService offlineService,
        IConnectivityService connectivity,
        ILogger<OfflineModelsViewModel> logger,
        IUsageMetricsService? usageMetrics = null)
    {
        _offlineService = offlineService;
        _connectivity = connectivity;
        _logger = logger;
        _usageMetrics = usageMetrics;
        Title = "Offline AI Models";

        _isOnline = _connectivity.IsConnected;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public async Task InitializeAsync()
    {
        _usageMetrics?.TrackFeatureUsed("OfflineModels");
        await LoadModelsAsync();
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        try
        {
            IsLoading = true;
            var availableModels = await _offlineService.GetAvailableModelsAsync();
            CurrentModelName = _offlineService.GetCurrentModelName();

            Models.Clear();
            foreach (var model in availableModels)
            {
                var viewModel = new LocalModelItemViewModel(model, _offlineService, _logger);
                Models.Add(viewModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading models");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnConnectivityChanged(object? sender, AI_Bible_App.Core.Services.ConnectivityChangedEventArgs e)
    {
        IsOnline = e.IsConnected;
    }
}

public partial class LocalModelItemViewModel : ObservableObject
{
    private readonly LocalModelInfo _model;
    private readonly IOfflineAIService _offlineService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string _sizeText;

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _recommendedFor;

    [ObservableProperty]
    private string _sizeCategory;

    [ObservableProperty]
    private bool _isCurrent;

    public LocalModelItemViewModel(LocalModelInfo model, IOfflineAIService offlineService, ILogger logger)
    {
        _model = model;
        _offlineService = offlineService;
        _logger = logger;

        Name = model.Name;
        DisplayName = model.DisplayName;
        Description = model.Description;
        IsDownloaded = model.IsDownloaded;
        RecommendedFor = model.RecommendedFor;
        IsCurrent = offlineService.GetCurrentModelName() == model.Name;

        // Format size
        var sizeInGB = model.SizeInBytes / (1024.0 * 1024.0 * 1024.0);
        SizeText = $"{sizeInGB:F1} GB";

        SizeCategory = model.Size switch
        {
            ModelSize.Tiny => "🚀 Ultra-Fast",
            ModelSize.Small => "⚡ Fast",
            ModelSize.Medium => "💪 Powerful",
            ModelSize.Large => "🎯 Best Quality",
            _ => "Standard"
        };
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        try
        {
            IsDownloading = true;
            DownloadProgress = 0;

            var progress = new Progress<double>(value =>
            {
                DownloadProgress = value * 100;
            });

            var success = await _offlineService.DownloadModelAsync(Name, progress);

            if (success)
            {
                IsDownloaded = true;
                _logger.LogInformation("Model {ModelName} downloaded successfully", DisplayName);
            }
            else
            {
                _logger.LogError("Failed to download model {ModelName}", DisplayName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading model {ModelName}", DisplayName);
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        try
        {
            var success = await _offlineService.DeleteModelAsync(Name);
            if (success)
            {
                IsDownloaded = false;
                IsCurrent = false;
                _logger.LogInformation("Model {ModelName} deleted", DisplayName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {ModelName}", DisplayName);
        }
    }

    [RelayCommand]
    private async Task SetAsActiveAsync()
    {
        try
        {
            if (!IsDownloaded)
            {
                _logger.LogWarning("Cannot activate model {ModelName} - not downloaded", DisplayName);
                return;
            }

            var success = await _offlineService.SwitchModelAsync(Name);
            if (success)
            {
                IsCurrent = true;
                _logger.LogInformation("Switched to model {ModelName}", DisplayName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to model {ModelName}", DisplayName);
        }
    }

    [RelayCommand]
    private async Task ViewRequirementsAsync()
    {
        try
        {
            var requirements = await _offlineService.GetModelRequirementsAsync(Name);
            
            var diskGB = requirements.DiskSpaceRequired / (1024.0 * 1024.0 * 1024.0);
            var ramGB = requirements.RamRequired / (1024.0 * 1024.0 * 1024.0);

            var message = $@"Model Requirements for {DisplayName}:

💾 Disk Space: {diskGB:F1} GB
🧠 RAM: {ramGB:F1} GB
🖥️ CPU: {requirements.MinimumCpu}
🎮 GPU: {(requirements.GpuRecommended ? "Recommended for best performance" : "Not required")}
⏱️ Load Time: ~{requirements.EstimatedLoadTime.TotalSeconds:F0} seconds";

            _logger.LogInformation("Requirements for {ModelName}: {Message}", DisplayName, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting requirements for {ModelName}", DisplayName);
        }
    }
}
