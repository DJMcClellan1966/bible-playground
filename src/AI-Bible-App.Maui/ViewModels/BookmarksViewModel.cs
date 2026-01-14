using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045

namespace AI_Bible_App.Maui.ViewModels;

public partial class BookmarksViewModel : BaseViewModel
{
    private readonly IVerseBookmarkRepository _bookmarkRepository;
    private readonly IDialogService _dialogService;
    private readonly IUserService _userService;
    private readonly IBibleLookupService _bibleLookupService;

    [ObservableProperty]
    private ObservableCollection<VerseBookmark> bookmarks = new();

    [ObservableProperty]
    private VerseBookmark? selectedBookmark;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string? selectedCategory;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string editNote = string.Empty;

    [ObservableProperty]
    private string editCategory = string.Empty;

    public List<string> Categories { get; } = new()
    {
        "All",
        "Comfort",
        "Strength",
        "Wisdom",
        "Love",
        "Faith",
        "Hope",
        "Peace",
        "Joy",
        "Guidance",
        "Praise",
        "Other"
    };

    public BookmarksViewModel(
        IVerseBookmarkRepository bookmarkRepository, 
        IDialogService dialogService, 
        IUserService userService,
        IBibleLookupService bibleLookupService)
    {
        _bookmarkRepository = bookmarkRepository;
        _dialogService = dialogService;
        _userService = userService;
        _bibleLookupService = bibleLookupService;
        Title = "My Bookmarks";
    }

    public async Task InitializeAsync()
    {
        await LoadBookmarksAsync();
    }

    [RelayCommand]
    private async Task LoadBookmarksAsync()
    {
        try
        {
            IsBusy = true;
            var userId = _userService.CurrentUser?.Id ?? "default";

            IEnumerable<VerseBookmark> results;
            
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                results = await _bookmarkRepository.SearchBookmarksAsync(userId, SearchText);
            }
            else if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All")
            {
                results = await _bookmarkRepository.GetBookmarksByCategoryAsync(userId, SelectedCategory);
            }
            else
            {
                results = await _bookmarkRepository.GetAllBookmarksAsync(userId);
            }

            Bookmarks = new ObservableCollection<VerseBookmark>(results);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Bookmarks] Error loading: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadBookmarksAsync();
    }

    [RelayCommand]
    private async Task FilterByCategoryAsync(string? category)
    {
        SelectedCategory = category;
        SearchText = string.Empty;
        await LoadBookmarksAsync();
    }

    [RelayCommand]
    private async Task ViewBookmarkAsync(VerseBookmark? bookmark)
    {
        if (bookmark == null) return;
        
        SelectedBookmark = bookmark;
        
        // Update last accessed
        bookmark.LastAccessedAt = DateTime.UtcNow;
        await _bookmarkRepository.UpdateBookmarkAsync(bookmark);
    }

    [RelayCommand]
    private void StartEditingBookmark()
    {
        if (SelectedBookmark == null) return;
        
        EditNote = SelectedBookmark.Note ?? string.Empty;
        EditCategory = SelectedBookmark.Category ?? "Other";
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveBookmarkAsync()
    {
        if (SelectedBookmark == null) return;
        
        try
        {
            SelectedBookmark.Note = EditNote;
            SelectedBookmark.Category = EditCategory;
            await _bookmarkRepository.UpdateBookmarkAsync(SelectedBookmark);
            
            IsEditing = false;
            await LoadBookmarksAsync();
            
            await _dialogService.ShowAlertAsync("✓ Saved", "Bookmark updated successfully.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Bookmarks] Error saving: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to save bookmark.", "OK");
        }
    }

    [RelayCommand]
    private void CancelEditing()
    {
        IsEditing = false;
        EditNote = string.Empty;
        EditCategory = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteBookmarkAsync(VerseBookmark? bookmark)
    {
        bookmark ??= SelectedBookmark;
        if (bookmark == null) return;
        
        var confirm = await _dialogService.ShowConfirmAsync(
            "Delete Bookmark",
            $"Are you sure you want to delete the bookmark for {bookmark.VerseReference}?",
            "Delete",
            "Cancel");
        
        if (confirm)
        {
            try
            {
                await _bookmarkRepository.DeleteBookmarkAsync(bookmark.Id);
                
                if (SelectedBookmark?.Id == bookmark.Id)
                {
                    SelectedBookmark = null;
                    IsEditing = false;
                }
                
                await LoadBookmarksAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Bookmarks] Error deleting: {ex.Message}");
                await _dialogService.ShowAlertAsync("Error", "Failed to delete bookmark.", "OK");
            }
        }
    }

    [RelayCommand]
    private async Task ShareBookmarkAsync()
    {
        if (SelectedBookmark == null) return;
        
        try
        {
            var shareText = $"📖 {SelectedBookmark.VerseReference}\n\n" +
                           $"\"{SelectedBookmark.VerseText}\"\n\n";
            
            if (!string.IsNullOrWhiteSpace(SelectedBookmark.Note))
            {
                shareText += $"📝 {SelectedBookmark.Note}\n\n";
            }
            
            shareText += "— Voices of Scripture";
            
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = shareText,
                Title = "Share Verse"
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Bookmarks] Error sharing: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        SelectedBookmark = null;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task AddBookmarkAsync()
    {
        // Prompt user for verse reference
        var reference = await _dialogService.ShowPromptAsync(
            "Add Bookmark",
            "Enter a Bible verse reference (e.g., John 3:16):");
        
        if (string.IsNullOrWhiteSpace(reference)) return;

        try
        {
            var userId = _userService.CurrentUser?.Id ?? "default";
            
            // Check if already bookmarked
            if (await _bookmarkRepository.IsVerseBookmarkedAsync(userId, reference))
            {
                await _dialogService.ShowAlertAsync("Already Bookmarked", "This verse is already in your bookmarks.", "OK");
                return;
            }


            // Try to parse the reference (e.g., "John 3:16")
            string verseText = "Verse text not found";
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(reference.Trim(), @"^(?<book>[1-3]?\s?[A-Za-z ]+)\s+(?<chapter>\d+):(?<verse>\d+)");
                if (match.Success)
                {
                    var book = match.Groups["book"].Value.Trim();
                    var chapter = int.Parse(match.Groups["chapter"].Value);
                    var verse = int.Parse(match.Groups["verse"].Value);
                    var result = await _bibleLookupService.LookupPassageAsync(book, chapter, verse);
                    if (result.Found && !string.IsNullOrWhiteSpace(result.Text))
                        verseText = result.Text;
                }
            }
            catch { /* fallback to default */ }

            var bookmark = new VerseBookmark
            {
                UserId = userId,
                VerseReference = reference,
                VerseText = verseText,
                Category = "Other",
                CreatedAt = DateTime.UtcNow
            };

            await _bookmarkRepository.AddBookmarkAsync(bookmark);
            await LoadBookmarksAsync();
            
            await _dialogService.ShowAlertAsync("✓ Added", $"{reference} has been bookmarked.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Bookmarks] Error adding: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to add bookmark.", "OK");
        }
    }
}
