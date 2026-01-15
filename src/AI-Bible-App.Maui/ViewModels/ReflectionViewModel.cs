using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045

namespace AI_Bible_App.Maui.ViewModels;

public partial class ReflectionViewModel : BaseViewModel
{
    private readonly IReflectionRepository _reflectionRepository;
    private readonly IDialogService _dialogService;
    private readonly IUserService _userService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private ObservableCollection<Reflection> reflections = new();

    [ObservableProperty]
    private Reflection? selectedReflection;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string editTitle = string.Empty;

    [ObservableProperty]
    private string editNotes = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ReflectionType? filterType;

    [ObservableProperty]
    private bool showFavoritesOnly;

    public ReflectionViewModel(
        IReflectionRepository reflectionRepository,
        IDialogService dialogService,
        IUserService userService,
        IUsageMetricsService? usageMetrics = null)
    {
        _reflectionRepository = reflectionRepository;
        _dialogService = dialogService;
        _userService = userService;
        _usageMetrics = usageMetrics;
        Title = "My Reflections";
    }

    public async Task InitializeAsync()
    {
        _usageMetrics?.TrackFeatureUsed("Reflections");
        await LoadReflectionsAsync();
    }

    [RelayCommand]
    private async Task LoadReflectionsAsync()
    {
        try
        {
            IsBusy = true;
            
            List<Reflection> results;
            
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                results = await _reflectionRepository.SearchReflectionsAsync(SearchText);
            }
            else if (ShowFavoritesOnly)
            {
                results = await _reflectionRepository.GetFavoriteReflectionsAsync();
            }
            else if (FilterType.HasValue)
            {
                results = await _reflectionRepository.GetReflectionsByTypeAsync(FilterType.Value);
            }
            else
            {
                results = await _reflectionRepository.GetAllReflectionsAsync();
            }
            
            Reflections = new ObservableCollection<Reflection>(results);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task FilterByType(string? typeString)
    {
        SearchText = string.Empty;
        ShowFavoritesOnly = false;
        
        if (string.IsNullOrEmpty(typeString))
        {
            FilterType = null;
        }
        else if (typeString.ToLower() == "favorites")
        {
            FilterType = null;
            ShowFavoritesOnly = true;
        }
        else if (Enum.TryParse<ReflectionType>(typeString, true, out var type))
        {
            FilterType = type;
        }
        else
        {
            FilterType = null;
        }
        
        await LoadReflectionsAsync();
    }

    [RelayCommand]
    private async Task CreateNewReflection()
    {
        var reflection = new Reflection
        {
            Title = "New Reflection",
            Type = ReflectionType.Custom,
            CreatedAt = DateTime.UtcNow
        };
        
        await _reflectionRepository.SaveReflectionAsync(reflection);
        await LoadReflectionsAsync();
        
        // Open for editing
        SelectedReflection = Reflections.FirstOrDefault(r => r.Id == reflection.Id);
        if (SelectedReflection != null)
        {
            await ViewReflection(SelectedReflection);
        }
    }

    [RelayCommand]
    private async Task ViewReflection(Reflection? reflection)
    {
        if (reflection == null) return;
        
        SelectedReflection = reflection;
        EditTitle = reflection.Title;
        EditNotes = reflection.PersonalNotes;
        IsEditing = true;

        var typeIcon = reflection.Type switch
        {
            ReflectionType.Chat => "💬",
            ReflectionType.Prayer => "🙏",
            ReflectionType.BibleVerse => "📖",
            _ => "✏️"
        };

        var savedContentPreview = reflection.SavedContent.Length > 500 
            ? reflection.SavedContent.Substring(0, 500) + "..." 
            : reflection.SavedContent;

        var content = $"{typeIcon} {reflection.Type}\n";
        if (!string.IsNullOrEmpty(reflection.CharacterName))
        {
            content += $"From: {reflection.CharacterName}\n";
        }
        content += $"Created: {reflection.CreatedAt.ToLocalTime():g}\n\n";
        content += $"── Saved Content ──\n{savedContentPreview}\n\n";
        content += $"── My Thoughts ──\n{(string.IsNullOrEmpty(reflection.PersonalNotes) ? "(No notes yet)" : reflection.PersonalNotes)}";

        var action = await _dialogService.ShowActionSheetAsync(
            reflection.Title,
            "Close",
            "Delete",
            reflection.IsFavorite ? "★ Remove from Favorites" : "☆ Add to Favorites",
            "Edit Notes",
            "🔗 Share");

        if (action == "Edit Notes")
        {
            // Use full-screen notes editor for better experience
            var editPage = new Views.EditNotesPage(reflection.Title, reflection.PersonalNotes);
            await Shell.Current.Navigation.PushModalAsync(new NavigationPage(editPage));
            
            var newNotes = await editPage.GetResultAsync();
            if (newNotes != null)
            {
                reflection.PersonalNotes = newNotes;
                reflection.UpdatedAt = DateTime.UtcNow;
                await _reflectionRepository.SaveReflectionAsync(reflection);
                await LoadReflectionsAsync();
            }
        }
        else if (action == "Delete")
        {
            var confirm = await _dialogService.ShowConfirmAsync(
                "Delete Reflection",
                "Are you sure you want to delete this reflection?",
                "Delete", "Cancel");
            
            if (confirm)
            {
                await _reflectionRepository.DeleteReflectionAsync(reflection.Id);
                await LoadReflectionsAsync();
            }
        }
        else if (action?.Contains("Favorites") == true)
        {
            reflection.IsFavorite = !reflection.IsFavorite;
            await _reflectionRepository.SaveReflectionAsync(reflection);
            await LoadReflectionsAsync();
        }
        else if (action == "🔗 Share")
        {
            await ShareReflectionAsync(reflection);
        }
        
        IsEditing = false;
        SelectedReflection = null;
    }

    private async Task ShareReflectionAsync(Reflection reflection)
    {
        try
        {
            var allUsers = await _userService.GetAllUsersAsync();
            var currentUserId = _userService.CurrentUser?.Id;
            
            // Filter out current user
            var otherUsers = allUsers.Where(u => u.Id != currentUserId).ToList();
            
            if (otherUsers.Count == 0)
            {
                await _dialogService.ShowAlertAsync("No Other Users", "Create additional user profiles to share reflections with them.");
                return;
            }

            // Build options list
            var options = new List<string> { "📢 Share with Everyone" };
            options.AddRange(otherUsers.Select(u => $"{u.AvatarEmoji ?? "👤"} {u.Name}"));
            options.Add("🚫 Stop Sharing");
            
            var result = await _dialogService.ShowActionSheetAsync(
                $"Share '{reflection.Title}'",
                "Cancel",
                null,
                options.ToArray());

            if (result == null || result == "Cancel") return;

            if (result == "📢 Share with Everyone")
            {
                reflection.IsSharedWithAll = true;
                reflection.SharedWithUserIds.Clear();
                await _reflectionRepository.SaveReflectionAsync(reflection);
                await _dialogService.ShowAlertAsync("Shared", "This reflection is now visible to all users on this device.");
            }
            else if (result == "🚫 Stop Sharing")
            {
                reflection.IsSharedWithAll = false;
                reflection.SharedWithUserIds.Clear();
                await _reflectionRepository.SaveReflectionAsync(reflection);
                await _dialogService.ShowAlertAsync("Private", "This reflection is now private to you.");
            }
            else
            {
                // Find selected user
                var selectedUser = otherUsers.FirstOrDefault(u => result.Contains(u.Name));
                if (selectedUser != null)
                {
                    reflection.IsSharedWithAll = false;
                    if (!reflection.SharedWithUserIds.Contains(selectedUser.Id))
                    {
                        reflection.SharedWithUserIds.Add(selectedUser.Id);
                    }
                    await _reflectionRepository.SaveReflectionAsync(reflection);
                    await _dialogService.ShowAlertAsync("Shared", $"This reflection is now shared with {selectedUser.Name}.");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Share reflection failed: {ex}");
            await _dialogService.ShowAlertAsync("Error", $"Failed to share: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ToggleFavorite(Reflection reflection)
    {
        reflection.IsFavorite = !reflection.IsFavorite;
        await _reflectionRepository.SaveReflectionAsync(reflection);
        await LoadReflectionsAsync();
    }

    [RelayCommand]
    private async Task DeleteReflection(Reflection reflection)
    {
        var confirm = await _dialogService.ShowConfirmAsync(
            "Delete Reflection",
            $"Delete '{reflection.Title}'?",
            "Delete", "Cancel");
        
        if (confirm)
        {
            await _reflectionRepository.DeleteReflectionAsync(reflection.Id);
            await LoadReflectionsAsync();
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        await LoadReflectionsAsync();
    }

    [RelayCommand]
    private async Task ClearFilter()
    {
        SearchText = string.Empty;
        FilterType = null;
        ShowFavoritesOnly = false;
        await LoadReflectionsAsync();
    }

    // Helper method to save content from chat/prayer pages
    public async Task SaveReflectionAsync(string title, string content, ReflectionType type, string? characterName = null, List<string>? bibleRefs = null)
    {
        var reflection = new Reflection
        {
            Title = title,
            SavedContent = content,
            Type = type,
            CharacterName = characterName,
            BibleReferences = bibleRefs ?? new(),
            CreatedAt = DateTime.UtcNow
        };
        
        await _reflectionRepository.SaveReflectionAsync(reflection);
        
        await _dialogService.ShowAlertAsync(
            "Saved! ✓",
            $"'{title}' has been saved to your reflections.");
    }
}
