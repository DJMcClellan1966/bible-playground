using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace AI_Bible_App.Maui.ViewModels;

// Local model for search display (avoids conflict with Infrastructure.Services.VerseSearchResult)
public class BibleVerseSearchResult
{
    public string Reference { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Relevance { get; set; }
    public bool IsPlaceholder { get; set; }
}

public partial class BibleReaderViewModel : BaseViewModel
{
    private readonly IBibleLookupService _bibleLookupService;
    private readonly IBibleVerseIndexService _verseIndexService;
    private readonly ICharacterVoiceService _voiceService;
    private readonly IDialogService _dialogService;
    private readonly IUsageMetricsService? _usageMetrics;
    
    // Default voice config for Bible reading
    private static readonly VoiceConfig _defaultBibleVoice = new()
    {
        Pitch = 1.0f,
        Rate = 0.9f,  // Slightly slower for scripture reading
        Volume = 1.0f,
        Description = "Scripture reader",
        Locale = "en-US"
    };
    
    // Bible book structure - Old Testament + New Testament
    private static readonly List<string> AllBooks = new()
    {
        // Old Testament
        "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy",
        "Joshua", "Judges", "Ruth", "1 Samuel", "2 Samuel",
        "1 Kings", "2 Kings", "1 Chronicles", "2 Chronicles",
        "Ezra", "Nehemiah", "Esther", "Job", "Psalms",
        "Proverbs", "Ecclesiastes", "Song of Solomon",
        "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel",
        "Hosea", "Joel", "Amos", "Obadiah", "Jonah",
        "Micah", "Nahum", "Habakkuk", "Zephaniah", "Haggai",
        "Zechariah", "Malachi",
        // New Testament
        "Matthew", "Mark", "Luke", "John", "Acts",
        "Romans", "1 Corinthians", "2 Corinthians", "Galatians",
        "Ephesians", "Philippians", "Colossians",
        "1 Thessalonians", "2 Thessalonians",
        "1 Timothy", "2 Timothy", "Titus", "Philemon",
        "Hebrews", "James", "1 Peter", "2 Peter",
        "1 John", "2 John", "3 John", "Jude", "Revelation"
    };
    
    // Chapter counts per book
    private static readonly Dictionary<string, int> ChapterCounts = new()
    {
        { "Genesis", 50 }, { "Exodus", 40 }, { "Leviticus", 27 }, { "Numbers", 36 }, { "Deuteronomy", 34 },
        { "Joshua", 24 }, { "Judges", 21 }, { "Ruth", 4 }, { "1 Samuel", 31 }, { "2 Samuel", 24 },
        { "1 Kings", 22 }, { "2 Kings", 25 }, { "1 Chronicles", 29 }, { "2 Chronicles", 36 },
        { "Ezra", 10 }, { "Nehemiah", 13 }, { "Esther", 10 }, { "Job", 42 }, { "Psalms", 150 },
        { "Proverbs", 31 }, { "Ecclesiastes", 12 }, { "Song of Solomon", 8 },
        { "Isaiah", 66 }, { "Jeremiah", 52 }, { "Lamentations", 5 }, { "Ezekiel", 48 }, { "Daniel", 12 },
        { "Hosea", 14 }, { "Joel", 3 }, { "Amos", 9 }, { "Obadiah", 1 }, { "Jonah", 4 },
        { "Micah", 7 }, { "Nahum", 3 }, { "Habakkuk", 3 }, { "Zephaniah", 3 }, { "Haggai", 2 },
        { "Zechariah", 14 }, { "Malachi", 4 },
        { "Matthew", 28 }, { "Mark", 16 }, { "Luke", 24 }, { "John", 21 }, { "Acts", 28 },
        { "Romans", 16 }, { "1 Corinthians", 16 }, { "2 Corinthians", 13 }, { "Galatians", 6 },
        { "Ephesians", 6 }, { "Philippians", 4 }, { "Colossians", 4 },
        { "1 Thessalonians", 5 }, { "2 Thessalonians", 3 },
        { "1 Timothy", 6 }, { "2 Timothy", 4 }, { "Titus", 3 }, { "Philemon", 1 },
        { "Hebrews", 13 }, { "James", 5 }, { "1 Peter", 5 }, { "2 Peter", 3 },
        { "1 John", 5 }, { "2 John", 1 }, { "3 John", 1 }, { "Jude", 1 }, { "Revelation", 22 }
    };
    
    [ObservableProperty]
    private ObservableCollection<string> books = new(AllBooks);
    
    [ObservableProperty]
    private ObservableCollection<int> chapters = new();
    
    [ObservableProperty]
    private string? selectedBook;
    
    [ObservableProperty]
    private int selectedChapter = 1;
    
    [ObservableProperty]
    private string searchQuery = string.Empty;
    
    [ObservableProperty]
    private bool isSearching;
    
    [ObservableProperty]
    private int searchResultCount;
    
    [ObservableProperty]
    private ObservableCollection<BibleVerseSearchResult> searchResults = new();
    
    [ObservableProperty]
    private ObservableCollection<BibleVerse> currentVerses = new();
    
    [ObservableProperty]
    private string currentChapterHeading = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<string> bookmarks = new();
    
    [ObservableProperty]
    private bool showBookmarks;
    
    public BibleReaderViewModel(
        IBibleLookupService bibleLookupService,
        IBibleVerseIndexService verseIndexService,
        ICharacterVoiceService voiceService,
        IDialogService dialogService,
        IUsageMetricsService? usageMetrics = null)
    {
        _bibleLookupService = bibleLookupService;
        _verseIndexService = verseIndexService;
        _voiceService = voiceService;
        _dialogService = dialogService;
        _usageMetrics = usageMetrics;
        Title = "Bible Reader";
    }
    
    public async Task InitializeAsync()
    {
        if (IsBusy) return;
        
        try
        {
            IsBusy = true;
            _usageMetrics?.TrackFeatureUsed("BibleReader");
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                ClearSearch();
            }
            
            // Initialize verse index if needed
            if (!_verseIndexService.IsInitialized)
            {
                await _verseIndexService.InitializeAsync();
            }
            
            // Default to Genesis 1
            SelectedBook = "Genesis";
            UpdateChapters();
            SelectedChapter = 1;
            await LoadChapter();
            
            // Load bookmarks from preferences
            LoadBookmarks();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BibleReader] Init error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to load Bible data.");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    partial void OnSelectedBookChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            UpdateChapters();
        }
    }
    
    private void UpdateChapters()
    {
        if (string.IsNullOrEmpty(SelectedBook)) return;
        
        if (ChapterCounts.TryGetValue(SelectedBook, out var count))
        {
            Chapters = new ObservableCollection<int>(Enumerable.Range(1, count));
            if (SelectedChapter > count || SelectedChapter < 1)
            {
                SelectedChapter = 1;
            }
        }
    }
    
    [RelayCommand]
    private async Task LoadChapter()
    {
        if (string.IsNullOrEmpty(SelectedBook) || IsBusy) return;
        
        try
        {
            IsBusy = true;
            CurrentVerses.Clear();
            CurrentChapterHeading = $"{SelectedBook} {SelectedChapter}";
            
            // Load all verses for the chapter
            var result = await _bibleLookupService.LookupPassageAsync(
                SelectedBook, SelectedChapter, 1, 200); // Get up to 200 verses
            
            if (result.Found && result.Verses.Any())
            {
                foreach (var verse in result.Verses.OrderBy(v => v.Verse))
                {
                    CurrentVerses.Add(verse);
                }
            }
            else
            {
                // Create placeholder for chapters without data
                CurrentVerses.Add(new BibleVerse
                {
                    Book = SelectedBook,
                    Chapter = SelectedChapter,
                    Verse = 1,
                    Text = "Verse data not available for this chapter.",
                    Translation = "N/A"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BibleReader] Load chapter error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;
        
        try
        {
            IsBusy = true;
            IsSearching = true;
            SearchResults.Clear();
            
            // Check if query is a verse reference (e.g., "John 3:16")
            var referenceMatch = Regex.Match(SearchQuery, @"^(\d?\s*\w+)\s+(\d+):(\d+)(?:-(\d+))?$", RegexOptions.IgnoreCase);
            if (referenceMatch.Success)
            {
                var book = referenceMatch.Groups[1].Value;
                var chapter = int.Parse(referenceMatch.Groups[2].Value);
                var verseStart = int.Parse(referenceMatch.Groups[3].Value);
                var verseEnd = referenceMatch.Groups[4].Success ? int.Parse(referenceMatch.Groups[4].Value) : (int?)null;
                
                var result = await _bibleLookupService.LookupPassageAsync(book, chapter, verseStart, verseEnd);
                if (result.Found)
                {
                    SearchResults.Add(new BibleVerseSearchResult
                    {
                        Reference = result.Reference,
                        Text = result.Text,
                        Relevance = 1.0
                    });
                }

                var normalizedBook = NormalizeBookName(book);
                if (!string.IsNullOrEmpty(normalizedBook))
                {
                    _usageMetrics?.TrackBibleSearch(normalizedBook, chapter);
                }
            }
            else
            {
                // Full-text search
                var results = await _verseIndexService.SearchVersesAsync(SearchQuery, 50);
                foreach (var result in results)
                {
                    SearchResults.Add(new BibleVerseSearchResult
                    {
                        Reference = result.Reference,
                        Text = result.Text,
                        Relevance = result.Relevance
                    });
                }

                var detectedBook = DetectBookFromQuery(SearchQuery)
                    ?? TryExtractBookFromReference(SearchResults.FirstOrDefault()?.Reference);
                if (!string.IsNullOrEmpty(detectedBook))
                {
                    _usageMetrics?.TrackBibleSearch(detectedBook);
                }
            }
            
            SearchResultCount = SearchResults.Count;
            if (SearchResultCount == 0)
            {
                SearchResults.Add(new BibleVerseSearchResult
                {
                    Reference = "No results",
                    Text = "Try a different term or a verse reference like John 3:16.",
                    Relevance = 0,
                    IsPlaceholder = true
                });
                SearchResultCount = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BibleReader] Search error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Search Error", "Could not complete search.");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private void ClearSearch()
    {
        IsSearching = false;
        SearchQuery = string.Empty;
        SearchResults.Clear();
        SearchResultCount = 0;
    }

    [RelayCommand]
    private void ClearSearchInput()
    {
        SearchQuery = string.Empty;
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IsSearching = false;
            SearchResults.Clear();
            SearchResultCount = 0;
        }
    }

    private static string? DetectBookFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        foreach (var book in AllBooks.OrderByDescending(b => b.Length))
        {
            if (query.StartsWith(book, StringComparison.OrdinalIgnoreCase))
            {
                return book;
            }
        }

        return null;
    }

    private static string? TryExtractBookFromReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var match = Regex.Match(reference, @"^(.+?)\s+\d+:\d+", RegexOptions.IgnoreCase);
        return match.Success ? NormalizeBookName(match.Groups[1].Value) : null;
    }

    private static string? NormalizeBookName(string book)
    {
        if (string.IsNullOrWhiteSpace(book))
            return null;

        var cleaned = book.Trim();
        foreach (var known in AllBooks)
        {
            if (string.Equals(known, cleaned, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return cleaned;
    }
    
    [RelayCommand]
    private async Task GoToVerse(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return;
        
        try
        {
            // Parse reference like "Genesis 1:1" or "John 3:16"
            var match = Regex.Match(reference, @"^(\d?\s*\w+(?:\s+\w+)?)\s+(\d+):(\d+)");
            if (match.Success)
            {
                var book = match.Groups[1].Value.Trim();
                var chapter = int.Parse(match.Groups[2].Value);
                
                // Find matching book
                var matchingBook = AllBooks.FirstOrDefault(b => 
                    b.Equals(book, StringComparison.OrdinalIgnoreCase) ||
                    b.StartsWith(book, StringComparison.OrdinalIgnoreCase));
                
                if (!string.IsNullOrEmpty(matchingBook))
                {
                    SelectedBook = matchingBook;
                    UpdateChapters();
                    SelectedChapter = chapter;
                    await LoadChapter();
                    ClearSearch();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BibleReader] GoToVerse error: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task ShowVerseActions(BibleVerse verse)
    {
        if (verse == null) return;
        
        var reference = $"{verse.Book} {verse.Chapter}:{verse.Verse}";
        var action = await _dialogService.ShowActionSheetAsync(
            reference,
            "Cancel",
            null,
            "📋 Copy Verse",
            "🔖 Bookmark",
            "🔊 Read Aloud",
            "💬 Discuss with Character",
            "📝 Add to Reflections");
        
        switch (action)
        {
            case "📋 Copy Verse":
                await Clipboard.Default.SetTextAsync($"{reference}\n{verse.Text}");
                await _dialogService.ShowAlertAsync("Copied", "Verse copied to clipboard.");
                break;
            case "🔖 Bookmark":
                await BookmarkVerse(reference);
                break;
            case "🔊 Read Aloud":
                await _voiceService.SpeakAsync(verse.Text, _defaultBibleVoice);
                break;
            case "💬 Discuss with Character":
                await Shell.Current.GoToAsync($"///CharacterSelection?discussVerse={Uri.EscapeDataString(reference)}");
                break;
            case "📝 Add to Reflections":
                // Navigate to reflection creation
                await Shell.Current.GoToAsync($"///Reflections?newReflection={Uri.EscapeDataString(reference)}");
                break;
        }
    }

    [RelayCommand]
    private async Task CopySearchResult(BibleVerseSearchResult result)
    {
        if (result == null) return;
        if (result.IsPlaceholder || string.Equals(result.Reference, "No results", StringComparison.OrdinalIgnoreCase))
            return;

        await Clipboard.Default.SetTextAsync($"{result.Reference}\n{result.Text}");
        await _dialogService.ShowAlertAsync("Copied", "Verse copied to clipboard.");
    }

    [RelayCommand]
    private async Task DiscussReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;
        if (string.Equals(reference, "No results", StringComparison.OrdinalIgnoreCase))
            return;

        await Shell.Current.GoToAsync($"///CharacterSelection?discussVerse={Uri.EscapeDataString(reference)}");
    }
    
    [RelayCommand]
    private async Task BookmarkVerse(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return;
        
        if (!Bookmarks.Contains(reference))
        {
            Bookmarks.Add(reference);
            SaveBookmarks();
            await _dialogService.ShowAlertAsync("Bookmarked", $"{reference} added to bookmarks.");
        }
        else
        {
            await _dialogService.ShowAlertAsync("Already Bookmarked", "This verse is already in your bookmarks.");
        }
    }
    
    [RelayCommand]
    private void ToggleBookmarks()
    {
        ShowBookmarks = !ShowBookmarks;
    }
    
    [RelayCommand]
    private async Task PreviousChapter()
    {
        if (SelectedChapter > 1)
        {
            SelectedChapter--;
            await LoadChapter();
        }
        else
        {
            // Go to previous book
            var currentIndex = AllBooks.IndexOf(SelectedBook ?? "Genesis");
            if (currentIndex > 0)
            {
                SelectedBook = AllBooks[currentIndex - 1];
                UpdateChapters();
                SelectedChapter = Chapters.Last();
                await LoadChapter();
            }
        }
    }
    
    [RelayCommand]
    private async Task NextChapter()
    {
        if (SelectedChapter < Chapters.Count)
        {
            SelectedChapter++;
            await LoadChapter();
        }
        else
        {
            // Go to next book
            var currentIndex = AllBooks.IndexOf(SelectedBook ?? "Genesis");
            if (currentIndex < AllBooks.Count - 1)
            {
                SelectedBook = AllBooks[currentIndex + 1];
                UpdateChapters();
                SelectedChapter = 1;
                await LoadChapter();
            }
        }
    }
    
    [RelayCommand]
    private async Task ReadAloud()
    {
        if (!CurrentVerses.Any()) return;
        
        var fullText = string.Join(" ", CurrentVerses.Select(v => $"Verse {v.Verse}. {v.Text}"));
        await _voiceService.SpeakAsync(fullText, _defaultBibleVoice);
    }
    
    [RelayCommand]
    private async Task CopyChapter()
    {
        if (!CurrentVerses.Any()) return;
        
        var fullText = $"{CurrentChapterHeading}\n\n" +
            string.Join("\n", CurrentVerses.Select(v => $"{v.Verse} {v.Text}"));
        await Clipboard.Default.SetTextAsync(fullText);
        await _dialogService.ShowAlertAsync("Copied", "Chapter copied to clipboard.");
    }
    
    [RelayCommand]
    private async Task DiscussWithCharacter()
    {
        var reference = $"{SelectedBook} {SelectedChapter}";
        await Shell.Current.GoToAsync($"///CharacterSelection?discussVerse={Uri.EscapeDataString(reference)}");
    }
    
    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("..");
    }
    
    private void LoadBookmarks()
    {
        try
        {
            var saved = Preferences.Default.Get("BibleBookmarks", string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                var items = saved.Split('|', StringSplitOptions.RemoveEmptyEntries);
                Bookmarks = new ObservableCollection<string>(items);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BibleReader] Load bookmarks error: {ex.Message}");
        }
    }
    
    private void SaveBookmarks()
    {
        try
        {
            var data = string.Join("|", Bookmarks);
            Preferences.Default.Set("BibleBookmarks", data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BibleReader] Save bookmarks error: {ex.Message}");
        }
    }
}
