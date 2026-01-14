using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AI_Bible_App.Maui.Services;

/// <summary>
/// Service for looking up Bible verses from local data
/// </summary>
public interface IBibleLookupService
{
    Task<BibleLookupResult> LookupPassageAsync(string book, int chapter, int verseStart, int? verseEnd = null);
    Task<string> GetContextualSummaryAsync(string reference, string verseText);
    Task<List<CharacterBibleReference>> GetCharacterReferencesAsync(BiblicalCharacter character, string topic);
}

/// <summary>
/// Represents a contextual Bible reference for a character
/// </summary>
public class CharacterBibleReference
{
    public string Reference { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Connection { get; set; } = string.Empty; // How character relates to this
}

public class BibleLookupResult
{
    public bool Found { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public List<BibleVerse> Verses { get; set; } = new();
}

public class BibleLookupService : IBibleLookupService
{
    private readonly IAIService _aiService;
    private readonly string _bibleDataPath;
    
    // Lazy loading: verses loaded on first access
    private readonly Lazy<Task<List<BibleVerse>>> _versesLoader;
    private List<BibleVerse>? _cachedVerses;
    
    // Performance: Index by book for O(1) lookup
    private Dictionary<string, List<BibleVerse>>? _versesByBook;
    
    // LRU cache for frequently accessed passages
    private readonly ConcurrentDictionary<string, BibleLookupResult> _passageCache = new();
    private const int MaxCacheSize = 100;

    // Book name normalization mapping
    private static readonly Dictionary<string, string> BookNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Standard names
        { "Genesis", "Genesis" }, { "Gen", "Genesis" }, { "Ge", "Genesis" },
        { "Exodus", "Exodus" }, { "Exod", "Exodus" }, { "Ex", "Exodus" },
        { "Leviticus", "Leviticus" }, { "Lev", "Leviticus" },
        { "Numbers", "Numbers" }, { "Num", "Numbers" },
        { "Deuteronomy", "Deuteronomy" }, { "Deut", "Deuteronomy" },
        { "Joshua", "Joshua" }, { "Josh", "Joshua" },
        { "Judges", "Judges" }, { "Judg", "Judges" },
        { "Ruth", "Ruth" },
        { "1 Samuel", "1 Samuel" }, { "1Samuel", "1 Samuel" }, { "1 Sam", "1 Samuel" }, { "1Sam", "1 Samuel" },
        { "2 Samuel", "2 Samuel" }, { "2Samuel", "2 Samuel" }, { "2 Sam", "2 Samuel" }, { "2Sam", "2 Samuel" },
        { "1 Kings", "1 Kings" }, { "1Kings", "1 Kings" }, { "1 Kgs", "1 Kings" },
        { "2 Kings", "2 Kings" }, { "2Kings", "2 Kings" }, { "2 Kgs", "2 Kings" },
        { "1 Chronicles", "1 Chronicles" }, { "1Chronicles", "1 Chronicles" }, { "1 Chr", "1 Chronicles" },
        { "2 Chronicles", "2 Chronicles" }, { "2Chronicles", "2 Chronicles" }, { "2 Chr", "2 Chronicles" },
        { "Ezra", "Ezra" },
        { "Nehemiah", "Nehemiah" }, { "Neh", "Nehemiah" },
        { "Esther", "Esther" }, { "Est", "Esther" },
        { "Job", "Job" },
        { "Psalms", "Psalms" }, { "Psalm", "Psalms" }, { "Ps", "Psalms" }, { "Psa", "Psalms" },
        { "Proverbs", "Proverbs" }, { "Prov", "Proverbs" }, { "Pro", "Proverbs" },
        { "Ecclesiastes", "Ecclesiastes" }, { "Eccl", "Ecclesiastes" }, { "Ecc", "Ecclesiastes" },
        { "Song of Solomon", "Song of Solomon" }, { "Song", "Song of Solomon" }, { "SoS", "Song of Solomon" },
        { "Isaiah", "Isaiah" }, { "Isa", "Isaiah" },
        { "Jeremiah", "Jeremiah" }, { "Jer", "Jeremiah" },
        { "Lamentations", "Lamentations" }, { "Lam", "Lamentations" },
        { "Ezekiel", "Ezekiel" }, { "Ezek", "Ezekiel" },
        { "Daniel", "Daniel" }, { "Dan", "Daniel" },
        { "Hosea", "Hosea" }, { "Hos", "Hosea" },
        { "Joel", "Joel" },
        { "Amos", "Amos" },
        { "Obadiah", "Obadiah" }, { "Obad", "Obadiah" },
        { "Jonah", "Jonah" },
        { "Micah", "Micah" }, { "Mic", "Micah" },
        { "Nahum", "Nahum" }, { "Nah", "Nahum" },
        { "Habakkuk", "Habakkuk" }, { "Hab", "Habakkuk" },
        { "Zephaniah", "Zephaniah" }, { "Zeph", "Zephaniah" },
        { "Haggai", "Haggai" }, { "Hag", "Haggai" },
        { "Zechariah", "Zechariah" }, { "Zech", "Zechariah" },
        { "Malachi", "Malachi" }, { "Mal", "Malachi" },
        // New Testament
        { "Matthew", "Matthew" }, { "Matt", "Matthew" }, { "Mt", "Matthew" },
        { "Mark", "Mark" }, { "Mk", "Mark" },
        { "Luke", "Luke" }, { "Lk", "Luke" },
        { "John", "John" }, { "Jn", "John" },
        { "Acts", "Acts" },
        { "Romans", "Romans" }, { "Rom", "Romans" },
        { "1 Corinthians", "1 Corinthians" }, { "1Corinthians", "1 Corinthians" }, { "1 Cor", "1 Corinthians" }, { "1Cor", "1 Corinthians" },
        { "2 Corinthians", "2 Corinthians" }, { "2Corinthians", "2 Corinthians" }, { "2 Cor", "2 Corinthians" }, { "2Cor", "2 Corinthians" },
        { "Galatians", "Galatians" }, { "Gal", "Galatians" },
        { "Ephesians", "Ephesians" }, { "Eph", "Ephesians" },
        { "Philippians", "Philippians" }, { "Phil", "Philippians" },
        { "Colossians", "Colossians" }, { "Col", "Colossians" },
        { "1 Thessalonians", "1 Thessalonians" }, { "1Thessalonians", "1 Thessalonians" }, { "1 Thess", "1 Thessalonians" }, { "1Thess", "1 Thessalonians" },
        { "2 Thessalonians", "2 Thessalonians" }, { "2Thessalonians", "2 Thessalonians" }, { "2 Thess", "2 Thessalonians" }, { "2Thess", "2 Thessalonians" },
        { "1 Timothy", "1 Timothy" }, { "1Timothy", "1 Timothy" }, { "1 Tim", "1 Timothy" }, { "1Tim", "1 Timothy" },
        { "2 Timothy", "2 Timothy" }, { "2Timothy", "2 Timothy" }, { "2 Tim", "2 Timothy" }, { "2Tim", "2 Timothy" },
        { "Titus", "Titus" },
        { "Philemon", "Philemon" }, { "Phlm", "Philemon" },
        { "Hebrews", "Hebrews" }, { "Heb", "Hebrews" },
        { "James", "James" }, { "Jas", "James" },
        { "1 Peter", "1 Peter" }, { "1Peter", "1 Peter" }, { "1 Pet", "1 Peter" }, { "1Pet", "1 Peter" },
        { "2 Peter", "2 Peter" }, { "2Peter", "2 Peter" }, { "2 Pet", "2 Peter" }, { "2Pet", "2 Peter" },
        { "1 John", "1 John" }, { "1John", "1 John" }, { "1 Jn", "1 John" },
        { "2 John", "2 John" }, { "2John", "2 John" }, { "2 Jn", "2 John" },
        { "3 John", "3 John" }, { "3John", "3 John" }, { "3 Jn", "3 John" },
        { "Jude", "Jude" },
        { "Revelation", "Revelation" }, { "Rev", "Revelation" }
    };

    public BibleLookupService(IAIService aiService)
    {
        _aiService = aiService;
        _bibleDataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Bible");
        
        // Lazy load: Bible data only loaded when first needed
        _versesLoader = new Lazy<Task<List<BibleVerse>>>(() => LoadVersesInternalAsync());
    }

    public async Task<BibleLookupResult> LookupPassageAsync(string book, int chapter, int verseStart, int? verseEnd = null)
    {
        var result = new BibleLookupResult
        {
            Reference = verseEnd.HasValue && verseEnd != verseStart
                ? $"{book} {chapter}:{verseStart}-{verseEnd}"
                : $"{book} {chapter}:{verseStart}"
        };

        // Check cache first
        var cacheKey = $"{book}:{chapter}:{verseStart}:{verseEnd ?? verseStart}";
        if (_passageCache.TryGetValue(cacheKey, out var cachedResult))
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Cache hit for {cacheKey}");
            return cachedResult;
        }

        try
        {
            // Ensure verses are loaded (lazy load on first access)
            await EnsureVersesLoadedAsync();

            if (_versesByBook == null || _versesByBook.Count == 0)
            {
                return result;
            }

            // Normalize book name
            var normalizedBook = NormalizeBookName(book);

            // Use indexed lookup (O(1) for book lookup)
            if (!_versesByBook.TryGetValue(normalizedBook.ToLowerInvariant(), out var bookVerses))
            {
                return result;
            }

            // Find matching verses within the book
            var endVerse = verseEnd ?? verseStart;
            var matchingVerses = bookVerses
                .Where(v => v.Chapter == chapter
                         && v.Verse >= verseStart
                         && v.Verse <= endVerse)
                .OrderBy(v => v.Verse)
                .ToList();

            if (matchingVerses.Any())
            {
                result.Found = true;
                result.Verses = matchingVerses;
                result.Translation = matchingVerses.First().Translation;
                result.Text = string.Join(" ", matchingVerses.Select(v => $"[{v.Verse}] {v.Text}"));
                
                // Add to cache (limit cache size)
                if (_passageCache.Count >= MaxCacheSize)
                {
                    // Remove first item (simple eviction)
                    var firstKey = _passageCache.Keys.FirstOrDefault();
                    if (firstKey != null) _passageCache.TryRemove(firstKey, out _);
                }
                _passageCache[cacheKey] = result;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error looking up passage: {ex.Message}");
        }

        return result;
    }

    private async Task EnsureVersesLoadedAsync()
    {
        if (_cachedVerses == null)
        {
            try
            {
                _cachedVerses = await _versesLoader.Value;
            }
            catch
            {
                _cachedVerses = new List<BibleVerse>();
            }
            // Build book index for fast lookups
            if (_cachedVerses != null && _cachedVerses.Any())
            {
                _versesByBook = _cachedVerses
                    .GroupBy(v => v.Book.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            else
            {
                _versesByBook = new Dictionary<string, List<BibleVerse>>();
            }
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Built index for {_versesByBook.Count} books");
        }
    }

    public async Task<string> GetContextualSummaryAsync(string reference, string verseText)
    {
        try
        {
            var prompt = $@"Provide a brief (2-3 sentence) contextual summary of this Bible passage. 
Include: the historical/literary context, the main message, and how it applies today.

Reference: {reference}
Text: {verseText}

Respond with just the summary, no preamble.";

            // Use a simple character for the summary request
            var dummyCharacter = new BiblicalCharacter
            {
                Name = "Bible Scholar",
                SystemPrompt = "You are a helpful Bible scholar who provides concise, accurate contextual summaries of Scripture passages."
            };

            var response = await _aiService.GetChatResponseAsync(
                dummyCharacter,
                new List<ChatMessage>(),
                prompt);

            return response;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting summary: {ex.Message}");
            return "Unable to generate summary.";
        }
    }

    public async Task<List<CharacterBibleReference>> GetCharacterReferencesAsync(BiblicalCharacter character, string topic)
    {
        var references = new List<CharacterBibleReference>();

        try
        {
            var prompt = $@"You are a Bible scholar. Given this biblical character and topic, identify 1-2 specific Bible verses where this character demonstrated knowledge or experience related to the topic.

Character: {character.Name}
Character Background: {character.Description}
Character's Biblical Books: {string.Join(", ", character.BiblicalReferences)}
Topic/Question: {topic}

For each reference, provide:
1. The exact Bible reference (e.g., ""Psalm 51:10"" or ""1 Samuel 17:45-47"")
2. A one-sentence summary of what the passage says
3. How this character personally experienced or demonstrated this

Format your response EXACTLY as follows (one reference per line, use | as separator):
REFERENCE|SUMMARY|CONNECTION

Example for David on forgiveness:
Psalm 51:10|David pleads with God for a clean heart after his sin with Bathsheba|I wrote this psalm after Nathan confronted me about my sin, experiencing God's mercy firsthand

Only include references that are actually about this character. If the character has no direct biblical connection to the topic, respond with NONE.";

            var dummyCharacter = new BiblicalCharacter
            {
                Name = "Bible Scholar",
                SystemPrompt = "You are a knowledgeable Bible scholar. Be precise with Bible references. Only cite verses that directly involve the specified character."
            };

            var response = await _aiService.GetChatResponseAsync(
                dummyCharacter,
                new List<ChatMessage>(),
                prompt);

            if (string.IsNullOrWhiteSpace(response) || response.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                return references;
            }

            // Parse the response
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    references.Add(new CharacterBibleReference
                    {
                        Reference = parts[0].Trim(),
                        Summary = parts[1].Trim(),
                        Connection = parts[2].Trim()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting character references: {ex.Message}");
        }

        return references;
    }

    private async Task<List<BibleVerse>> LoadVersesInternalAsync()
    {
        var verses = new List<BibleVerse>();

        // Try loading as MAUI bundled assets first (correct for packaged apps)
        var bibleFiles = new[] { "web.json", "kjv.json", "asv.json" };
        bool loadedFromBundle = false;

        System.Diagnostics.Debug.WriteLine("[DEBUG] Starting lazy load of Bible data...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var fileName in bibleFiles)
        {
            try
            {
                // MAUI way: Use FileSystem.OpenAppPackageFileAsync for bundled assets
                using var stream = await FileSystem.OpenAppPackageFileAsync($"Data/Bible/{fileName}");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                var loadedVerses = System.Text.Json.JsonSerializer.Deserialize<List<BibleVerse>>(json);
                if (loadedVerses != null)
                {
                    verses.AddRange(loadedVerses);
                    loadedFromBundle = true;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {loadedVerses.Count} verses from bundled {fileName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Could not load bundled {fileName}: {ex.Message}");
            }
        }

        // Fallback: Try file system path (for development/debugging)
        if (!loadedFromBundle && Directory.Exists(_bibleDataPath))
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Trying file system fallback at: {_bibleDataPath}");
            foreach (var file in Directory.GetFiles(_bibleDataPath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var loadedVerses = System.Text.Json.JsonSerializer.Deserialize<List<BibleVerse>>(json);
                    if (loadedVerses != null)
                    {
                        verses.AddRange(loadedVerses);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {loadedVerses.Count} verses from {file}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Error loading {file}: {ex.Message}");
                }
            }
        }

        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"[DEBUG] Total verses loaded: {verses.Count} in {sw.ElapsedMilliseconds}ms");
        
        return verses;
    }

    private static string NormalizeBookName(string book)
    {
        // Clean up the book name
        var cleaned = book.Trim();
        
        // Handle numbered books (1 John, 2 Corinthians, etc.)
        cleaned = Regex.Replace(cleaned, @"^(I|II|III)\s+", m =>
        {
            return m.Groups[1].Value switch
            {
                "I" => "1 ",
                "II" => "2 ",
                "III" => "3 ",
                _ => m.Value
            };
        });

        // Look up in our map
        if (BookNameMap.TryGetValue(cleaned, out var normalized))
        {
            return normalized;
        }

        // Try without spaces for numbered books
        var noSpace = Regex.Replace(cleaned, @"^(\d)\s+", "$1");
        if (BookNameMap.TryGetValue(noSpace, out normalized))
        {
            return normalized;
        }

        return cleaned;
    }
}
