using System.Text.RegularExpressions;
using System.Text.Json;

namespace AI_Bible_App.Maui.Services.Core;

/// <summary>
/// Intelligent Scripture Context Engine that automatically finds and provides
/// relevant biblical passages based on conversation topics and character context.
/// </summary>
public interface IScriptureContextEngine
{
    Task<ScriptureContext> GetRelevantScripturesAsync(string topic, string? characterName = null);
    Task<List<ScriptureReference>> SearchScripturesAsync(string query, int maxResults = 5);
    Task<ScripturePassage?> GetPassageAsync(string reference);
    Task<List<ScriptureReference>> GetCharacterKeyVersesAsync(string characterName);
    Task<TopicalIndex> GetTopicalIndexAsync(string topic);
    Task PreloadCommonScripturesAsync();
}

public class ScriptureContext
{
    public string Topic { get; set; } = "";
    public List<ScripturePassage> PrimaryPassages { get; set; } = new();
    public List<ScripturePassage> SupportingPassages { get; set; } = new();
    public List<string> RelatedTopics { get; set; } = new();
    public string? CharacterConnection { get; set; }
    public double RelevanceScore { get; set; }
}

public class ScripturePassage
{
    public string Reference { get; set; } = "";
    public string Text { get; set; } = "";
    public string Book { get; set; } = "";
    public int Chapter { get; set; }
    public int StartVerse { get; set; }
    public int? EndVerse { get; set; }
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string? Context { get; set; }
}

public class ScriptureReference
{
    public string Reference { get; set; } = "";
    public double RelevanceScore { get; set; }
    public string[] MatchedKeywords { get; set; } = Array.Empty<string>();
}

public class TopicalIndex
{
    public string Topic { get; set; } = "";
    public List<string> PrimaryReferences { get; set; } = new();
    public List<string> SecondaryReferences { get; set; } = new();
    public Dictionary<string, List<string>> SubTopics { get; set; } = new();
    public List<string> RelatedTopics { get; set; } = new();
}

public partial class ScriptureContextEngine : IScriptureContextEngine
{
    private readonly Dictionary<string, TopicalIndex> _topicalIndex;
    private readonly Dictionary<string, List<string>> _characterVerses;
    private readonly Dictionary<string, ScripturePassage> _passageCache;
    private readonly string _biblePath;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public ScriptureContextEngine()
    {
        _topicalIndex = InitializeTopicalIndex();
        _characterVerses = InitializeCharacterVerses();
        _passageCache = new Dictionary<string, ScripturePassage>();
        _biblePath = Path.Combine(AppContext.BaseDirectory, "bible");
    }

    public async Task<ScriptureContext> GetRelevantScripturesAsync(string topic, string? characterName = null)
    {
        await EnsureInitializedAsync();
        
        var context = new ScriptureContext { Topic = topic };
        var keywords = ExtractKeywords(topic);
        
        // Find matching topics in index
        var matchedTopics = FindMatchingTopics(keywords);
        
        // Get primary passages from matched topics
        foreach (var matchedTopic in matchedTopics.Take(3))
        {
            if (_topicalIndex.TryGetValue(matchedTopic, out var index))
            {
                foreach (var reference in index.PrimaryReferences.Take(2))
                {
                    var passage = await GetPassageAsync(reference);
                    if (passage != null)
                    {
                        context.PrimaryPassages.Add(passage);
                    }
                }
                context.RelatedTopics.AddRange(index.RelatedTopics);
            }
        }
        
        // Add character-specific verses if applicable
        if (!string.IsNullOrEmpty(characterName))
        {
            var charVerses = await GetCharacterKeyVersesAsync(characterName);
            foreach (var verse in charVerses.Where(v => 
                keywords.Any(k => v.MatchedKeywords.Any(m => 
                    m.Contains(k, StringComparison.OrdinalIgnoreCase)))).Take(2))
            {
                var passage = await GetPassageAsync(verse.Reference);
                if (passage != null && !context.PrimaryPassages.Any(p => p.Reference == passage.Reference))
                {
                    context.SupportingPassages.Add(passage);
                }
            }
            
            context.CharacterConnection = GetCharacterScriptureConnection(characterName, topic);
        }
        
        // Calculate relevance score
        context.RelevanceScore = CalculateContextRelevance(context, keywords);
        
        return context;
    }

    public async Task<List<ScriptureReference>> SearchScripturesAsync(string query, int maxResults = 5)
    {
        await EnsureInitializedAsync();
        
        var keywords = ExtractKeywords(query);
        var results = new List<ScriptureReference>();
        
        // Search topical index
        foreach (var (topic, index) in _topicalIndex)
        {
            var topicKeywords = ExtractKeywords(topic);
            var overlap = keywords.Intersect(topicKeywords, StringComparer.OrdinalIgnoreCase).Count();
            
            if (overlap > 0)
            {
                var score = (double)overlap / Math.Max(keywords.Count, topicKeywords.Count);
                foreach (var reference in index.PrimaryReferences)
                {
                    results.Add(new ScriptureReference
                    {
                        Reference = reference,
                        RelevanceScore = score,
                        MatchedKeywords = keywords.Intersect(topicKeywords, StringComparer.OrdinalIgnoreCase).ToArray()
                    });
                }
            }
        }
        
        return results
            .GroupBy(r => r.Reference)
            .Select(g => new ScriptureReference
            {
                Reference = g.Key,
                RelevanceScore = g.Max(r => r.RelevanceScore),
                MatchedKeywords = g.SelectMany(r => r.MatchedKeywords).Distinct().ToArray()
            })
            .OrderByDescending(r => r.RelevanceScore)
            .Take(maxResults)
            .ToList();
    }

    public async Task<ScripturePassage?> GetPassageAsync(string reference)
    {
        if (_passageCache.TryGetValue(reference, out var cached))
            return cached;
        
        var parsed = ParseReference(reference);
        if (parsed == null) return null;
        
        var passage = await LoadPassageFromFileAsync(parsed.Value.book, parsed.Value.chapter, 
            parsed.Value.startVerse, parsed.Value.endVerse);
        
        if (passage != null)
        {
            _passageCache[reference] = passage;
        }
        
        return passage;
    }

    public Task<List<ScriptureReference>> GetCharacterKeyVersesAsync(string characterName)
    {
        var normalizedName = characterName.ToLowerInvariant();
        var results = new List<ScriptureReference>();
        
        foreach (var (key, verses) in _characterVerses)
        {
            if (normalizedName.Contains(key) || key.Contains(normalizedName))
            {
                results.AddRange(verses.Select(v => new ScriptureReference
                {
                    Reference = v,
                    RelevanceScore = 1.0,
                    MatchedKeywords = new[] { key }
                }));
            }
        }
        
        return Task.FromResult(results);
    }

    public Task<TopicalIndex> GetTopicalIndexAsync(string topic)
    {
        var normalized = topic.ToLowerInvariant();
        
        // Exact match
        if (_topicalIndex.TryGetValue(normalized, out var index))
            return Task.FromResult(index);
        
        // Partial match
        var partial = _topicalIndex.FirstOrDefault(kv => 
            kv.Key.Contains(normalized) || normalized.Contains(kv.Key));
        
        if (partial.Value != null)
            return Task.FromResult(partial.Value);
        
        // Return empty index
        return Task.FromResult(new TopicalIndex { Topic = topic });
    }

    public async Task PreloadCommonScripturesAsync()
    {
        await EnsureInitializedAsync();
        
        // Preload most commonly referenced passages
        var commonReferences = new[]
        {
            "John 3:16", "Romans 8:28", "Psalm 23:1-6", "Jeremiah 29:11",
            "Philippians 4:13", "Proverbs 3:5-6", "Isaiah 40:31", "Romans 12:1-2",
            "Matthew 28:19-20", "1 Corinthians 13:4-7", "Galatians 5:22-23"
        };
        
        foreach (var reference in commonReferences)
        {
            await GetPassageAsync(reference);
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            
            // Any initialization logic here
            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private Dictionary<string, TopicalIndex> InitializeTopicalIndex()
    {
        return new Dictionary<string, TopicalIndex>(StringComparer.OrdinalIgnoreCase)
        {
            ["love"] = new TopicalIndex
            {
                Topic = "Love",
                PrimaryReferences = new List<string> 
                { 
                    "1 Corinthians 13:4-7", "John 3:16", "1 John 4:8", 
                    "Romans 5:8", "John 15:13" 
                },
                SecondaryReferences = new List<string>
                {
                    "Matthew 22:37-39", "1 John 4:19", "Ephesians 5:25",
                    "Song of Solomon 8:6-7", "Colossians 3:14"
                },
                RelatedTopics = new List<string> { "grace", "forgiveness", "compassion", "mercy" }
            },
            
            ["faith"] = new TopicalIndex
            {
                Topic = "Faith",
                PrimaryReferences = new List<string>
                {
                    "Hebrews 11:1", "Romans 10:17", "James 2:17",
                    "Hebrews 11:6", "Mark 11:22-24"
                },
                SecondaryReferences = new List<string>
                {
                    "Matthew 17:20", "Galatians 2:20", "2 Corinthians 5:7",
                    "Romans 1:17", "Ephesians 2:8-9"
                },
                RelatedTopics = new List<string> { "trust", "belief", "hope", "salvation" }
            },
            
            ["forgiveness"] = new TopicalIndex
            {
                Topic = "Forgiveness",
                PrimaryReferences = new List<string>
                {
                    "Ephesians 4:32", "Colossians 3:13", "Matthew 6:14-15",
                    "1 John 1:9", "Psalm 103:12"
                },
                SecondaryReferences = new List<string>
                {
                    "Matthew 18:21-22", "Luke 23:34", "Acts 3:19",
                    "Isaiah 1:18", "Micah 7:18-19"
                },
                RelatedTopics = new List<string> { "grace", "mercy", "redemption", "reconciliation" }
            },
            
            ["hope"] = new TopicalIndex
            {
                Topic = "Hope",
                PrimaryReferences = new List<string>
                {
                    "Romans 15:13", "Jeremiah 29:11", "Romans 8:24-25",
                    "Hebrews 6:19", "1 Peter 1:3"
                },
                SecondaryReferences = new List<string>
                {
                    "Psalm 42:11", "Lamentations 3:21-23", "Romans 5:3-5",
                    "Isaiah 40:31", "Titus 2:13"
                },
                RelatedTopics = new List<string> { "faith", "trust", "future", "perseverance" }
            },
            
            ["prayer"] = new TopicalIndex
            {
                Topic = "Prayer",
                PrimaryReferences = new List<string>
                {
                    "Matthew 6:9-13", "Philippians 4:6-7", "1 Thessalonians 5:17",
                    "James 5:16", "John 14:13-14"
                },
                SecondaryReferences = new List<string>
                {
                    "Matthew 7:7-8", "Mark 11:24", "Romans 8:26",
                    "1 John 5:14-15", "Psalm 145:18"
                },
                RelatedTopics = new List<string> { "worship", "intercession", "faith", "communion" }
            },
            
            ["salvation"] = new TopicalIndex
            {
                Topic = "Salvation",
                PrimaryReferences = new List<string>
                {
                    "John 3:16", "Romans 10:9-10", "Ephesians 2:8-9",
                    "Acts 4:12", "Romans 6:23"
                },
                SecondaryReferences = new List<string>
                {
                    "John 14:6", "Acts 16:31", "Titus 3:5",
                    "2 Corinthians 5:17", "1 Peter 1:18-19"
                },
                RelatedTopics = new List<string> { "redemption", "grace", "faith", "eternal life" }
            },
            
            ["peace"] = new TopicalIndex
            {
                Topic = "Peace",
                PrimaryReferences = new List<string>
                {
                    "John 14:27", "Philippians 4:6-7", "Isaiah 26:3",
                    "Romans 5:1", "Colossians 3:15"
                },
                SecondaryReferences = new List<string>
                {
                    "Psalm 29:11", "John 16:33", "Romans 8:6",
                    "Isaiah 9:6", "Matthew 5:9"
                },
                RelatedTopics = new List<string> { "rest", "trust", "comfort", "security" }
            },
            
            ["wisdom"] = new TopicalIndex
            {
                Topic = "Wisdom",
                PrimaryReferences = new List<string>
                {
                    "Proverbs 3:5-6", "James 1:5", "Proverbs 9:10",
                    "Colossians 2:3", "Proverbs 2:6"
                },
                SecondaryReferences = new List<string>
                {
                    "Proverbs 4:7", "1 Corinthians 1:30", "Ecclesiastes 7:12",
                    "Job 28:28", "Proverbs 16:16"
                },
                RelatedTopics = new List<string> { "knowledge", "understanding", "guidance", "discernment" }
            },
            
            ["suffering"] = new TopicalIndex
            {
                Topic = "Suffering",
                PrimaryReferences = new List<string>
                {
                    "Romans 8:28", "2 Corinthians 1:3-4", "James 1:2-4",
                    "1 Peter 4:12-13", "Romans 5:3-5"
                },
                SecondaryReferences = new List<string>
                {
                    "Psalm 34:18", "2 Corinthians 4:17", "Hebrews 12:11",
                    "Isaiah 43:2", "Revelation 21:4"
                },
                RelatedTopics = new List<string> { "trials", "perseverance", "comfort", "hope" }
            },
            
            ["holy spirit"] = new TopicalIndex
            {
                Topic = "Holy Spirit",
                PrimaryReferences = new List<string>
                {
                    "John 14:26", "Acts 1:8", "Galatians 5:22-23",
                    "Romans 8:26", "Ephesians 5:18"
                },
                SecondaryReferences = new List<string>
                {
                    "John 16:13", "1 Corinthians 12:4-11", "Romans 8:11",
                    "Acts 2:38", "2 Corinthians 3:17"
                },
                RelatedTopics = new List<string> { "gifts", "power", "guidance", "fruit" }
            }
        };
    }

    private Dictionary<string, List<string>> InitializeCharacterVerses()
    {
        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["peter"] = new List<string>
            {
                "Matthew 16:18", "John 21:15-17", "Acts 2:14-41",
                "1 Peter 2:9", "1 Peter 5:7", "Acts 3:6"
            },
            ["paul"] = new List<string>
            {
                "Philippians 4:13", "Romans 8:28", "2 Corinthians 12:9",
                "Galatians 2:20", "Philippians 3:13-14", "Acts 9:1-19"
            },
            ["john"] = new List<string>
            {
                "John 3:16", "1 John 4:8", "John 1:1-5",
                "1 John 1:9", "Revelation 21:1-4", "John 15:13"
            },
            ["moses"] = new List<string>
            {
                "Exodus 3:14", "Deuteronomy 6:4-5", "Exodus 20:1-17",
                "Numbers 6:24-26", "Deuteronomy 31:6", "Exodus 14:13-14"
            },
            ["david"] = new List<string>
            {
                "Psalm 23:1-6", "Psalm 51:10", "1 Samuel 17:47",
                "Psalm 139:14", "Psalm 27:1", "2 Samuel 22:2-4"
            },
            ["jesus"] = new List<string>
            {
                "John 14:6", "Matthew 11:28-30", "John 10:10",
                "Matthew 28:18-20", "John 8:12", "Luke 4:18-19"
            },
            ["mary magdalene"] = new List<string>
            {
                "John 20:11-18", "Luke 8:2", "Mark 16:9",
                "Matthew 27:56", "John 19:25", "Luke 24:10"
            },
            ["abraham"] = new List<string>
            {
                "Genesis 12:1-3", "Genesis 15:6", "Hebrews 11:8-12",
                "Romans 4:3", "Genesis 22:8", "Galatians 3:6-9"
            }
        };
    }

    private List<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string> 
        { 
            "the", "a", "an", "is", "are", "was", "what", "how", "why", "when",
            "where", "who", "which", "this", "that", "can", "could", "would",
            "should", "do", "does", "have", "has", "be", "to", "of", "in", "for",
            "on", "with", "at", "by", "from", "about", "tell", "me", "us", "i"
        };
        
        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToList();
    }

    private List<string> FindMatchingTopics(List<string> keywords)
    {
        var scores = new Dictionary<string, double>();
        
        foreach (var (topic, index) in _topicalIndex)
        {
            var topicWords = ExtractKeywords(topic);
            topicWords.AddRange(index.RelatedTopics.SelectMany(ExtractKeywords));
            
            var matches = keywords.Count(k => 
                topicWords.Any(tw => tw.Contains(k) || k.Contains(tw)));
            
            if (matches > 0)
            {
                scores[topic] = (double)matches / keywords.Count;
            }
        }
        
        return scores.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

    private (string book, int chapter, int startVerse, int? endVerse)? ParseReference(string reference)
    {
        // Pattern: "Book Chapter:Verse" or "Book Chapter:StartVerse-EndVerse"
        var pattern = ReferencePattern();
        var match = pattern.Match(reference);
        
        if (!match.Success) return null;
        
        var book = match.Groups[1].Value.Trim();
        var chapter = int.Parse(match.Groups[2].Value);
        var startVerse = int.Parse(match.Groups[3].Value);
        int? endVerse = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : null;
        
        return (book, chapter, startVerse, endVerse);
    }

    [GeneratedRegex(@"^(.+?)\s+(\d+):(\d+)(?:-(\d+))?$")]
    private static partial Regex ReferencePattern();

    private async Task<ScripturePassage?> LoadPassageFromFileAsync(string book, int chapter, 
        int startVerse, int? endVerse)
    {
        try
        {
            var bookCode = GetBookCode(book);
            if (string.IsNullOrEmpty(bookCode)) return null;
            
            var fileName = $"{bookCode}{chapter:D2}.htm";
            var filePath = Path.Combine(_biblePath, fileName);
            
            if (!File.Exists(filePath))
            {
                // Try without leading zero
                fileName = $"{bookCode}{chapter}.htm";
                filePath = Path.Combine(_biblePath, fileName);
                
                if (!File.Exists(filePath))
                    return CreateFallbackPassage(book, chapter, startVerse, endVerse);
            }
            
            var html = await File.ReadAllTextAsync(filePath);
            var text = ExtractVerseText(html, startVerse, endVerse ?? startVerse);
            
            return new ScripturePassage
            {
                Reference = endVerse.HasValue 
                    ? $"{book} {chapter}:{startVerse}-{endVerse}"
                    : $"{book} {chapter}:{startVerse}",
                Text = text ?? $"[{book} {chapter}:{startVerse}]",
                Book = book,
                Chapter = chapter,
                StartVerse = startVerse,
                EndVerse = endVerse,
                Keywords = ExtractKeywords(text ?? "").ToArray()
            };
        }
        catch
        {
            return CreateFallbackPassage(book, chapter, startVerse, endVerse);
        }
    }

    private ScripturePassage CreateFallbackPassage(string book, int chapter, int startVerse, int? endVerse)
    {
        return new ScripturePassage
        {
            Reference = endVerse.HasValue 
                ? $"{book} {chapter}:{startVerse}-{endVerse}"
                : $"{book} {chapter}:{startVerse}",
            Text = $"[Reference: {book} {chapter}:{startVerse}{(endVerse.HasValue ? $"-{endVerse}" : "")}]",
            Book = book,
            Chapter = chapter,
            StartVerse = startVerse,
            EndVerse = endVerse
        };
    }

    private string? ExtractVerseText(string html, int startVerse, int endVerse)
    {
        var verses = new List<string>();
        
        for (int v = startVerse; v <= endVerse; v++)
        {
            // Try to find verse markers in HTML
            var versePattern = new Regex($@"<sup[^>]*>{v}</sup>\s*([^<]+)", RegexOptions.IgnoreCase);
            var match = versePattern.Match(html);
            
            if (match.Success)
            {
                verses.Add(match.Groups[1].Value.Trim());
            }
            else
            {
                // Alternative pattern
                var altPattern = new Regex($@"\b{v}\b\s+([^0-9<]+)", RegexOptions.IgnoreCase);
                var altMatch = altPattern.Match(html);
                if (altMatch.Success)
                {
                    verses.Add(altMatch.Groups[1].Value.Trim());
                }
            }
        }
        
        return verses.Count > 0 ? string.Join(" ", verses) : null;
    }

    private string? GetBookCode(string book)
    {
        var codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Genesis"] = "GEN", ["Exodus"] = "EXO", ["Leviticus"] = "LEV",
            ["Numbers"] = "NUM", ["Deuteronomy"] = "DEU", ["Joshua"] = "JOS",
            ["Judges"] = "JDG", ["Ruth"] = "RUT", ["1 Samuel"] = "1SA",
            ["2 Samuel"] = "2SA", ["1 Kings"] = "1KI", ["2 Kings"] = "2KI",
            ["1 Chronicles"] = "1CH", ["2 Chronicles"] = "2CH", ["Ezra"] = "EZR",
            ["Nehemiah"] = "NEH", ["Esther"] = "EST", ["Job"] = "JOB",
            ["Psalm"] = "PSA", ["Psalms"] = "PSA", ["Proverbs"] = "PRO",
            ["Ecclesiastes"] = "ECC", ["Song of Solomon"] = "SNG", ["Isaiah"] = "ISA",
            ["Jeremiah"] = "JER", ["Lamentations"] = "LAM", ["Ezekiel"] = "EZE",
            ["Daniel"] = "DAN", ["Hosea"] = "HOS", ["Joel"] = "JOL",
            ["Amos"] = "AMO", ["Obadiah"] = "OBA", ["Jonah"] = "JON",
            ["Micah"] = "MIC", ["Nahum"] = "NAH", ["Habakkuk"] = "HAB",
            ["Zephaniah"] = "ZEP", ["Haggai"] = "HAG", ["Zechariah"] = "ZEC",
            ["Malachi"] = "MAL", ["Matthew"] = "MAT", ["Mark"] = "MAR",
            ["Luke"] = "LUK", ["John"] = "JOH", ["Acts"] = "ACT",
            ["Romans"] = "ROM", ["1 Corinthians"] = "1CO", ["2 Corinthians"] = "2CO",
            ["Galatians"] = "GAL", ["Ephesians"] = "EPH", ["Philippians"] = "PHP",
            ["Colossians"] = "COL", ["1 Thessalonians"] = "1TH", ["2 Thessalonians"] = "2TH",
            ["1 Timothy"] = "1TI", ["2 Timothy"] = "2TI", ["Titus"] = "TIT",
            ["Philemon"] = "PHM", ["Hebrews"] = "HEB", ["James"] = "JAM",
            ["1 Peter"] = "1PE", ["2 Peter"] = "2PE", ["1 John"] = "1JN",
            ["2 John"] = "2JN", ["3 John"] = "3JN", ["Jude"] = "JUD",
            ["Revelation"] = "REV"
        };
        
        return codes.GetValueOrDefault(book);
    }

    private string? GetCharacterScriptureConnection(string characterName, string topic)
    {
        var connections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["peter"] = new Dictionary<string, string>
            {
                ["faith"] = "Peter walked on water by faith, showing both its power and the importance of keeping our eyes on Jesus",
                ["forgiveness"] = "Peter experienced profound forgiveness after denying Jesus three times",
                ["leadership"] = "Jesus commissioned Peter to shepherd His flock and lead the early church"
            },
            ["paul"] = new Dictionary<string, string>
            {
                ["grace"] = "Paul's dramatic conversion demonstrates the transforming power of God's grace",
                ["suffering"] = "Paul learned that God's power is made perfect in weakness",
                ["mission"] = "Paul devoted his life to spreading the gospel to the Gentiles"
            },
            ["john"] = new Dictionary<string, string>
            {
                ["love"] = "John was known as the disciple whom Jesus loved and emphasized love in his writings",
                ["truth"] = "John's gospel reveals Jesus as the way, the truth, and the life",
                ["eternal life"] = "John wrote extensively about eternal life through believing in Christ"
            }
        };
        
        var normalizedName = characterName.ToLowerInvariant();
        foreach (var (key, topicConnections) in connections)
        {
            if (normalizedName.Contains(key))
            {
                foreach (var (topicKey, connection) in topicConnections)
                {
                    if (topic.Contains(topicKey, StringComparison.OrdinalIgnoreCase))
                        return connection;
                }
            }
        }
        
        return null;
    }

    private double CalculateContextRelevance(ScriptureContext context, List<string> keywords)
    {
        var score = 0.0;
        
        // Score based on passage count
        score += context.PrimaryPassages.Count * 0.2;
        score += context.SupportingPassages.Count * 0.1;
        
        // Score based on keyword matches in passages
        foreach (var passage in context.PrimaryPassages)
        {
            var matches = keywords.Count(k => 
                passage.Keywords.Any(pk => pk.Contains(k, StringComparison.OrdinalIgnoreCase)));
            score += matches * 0.1;
        }
        
        // Bonus for character connection
        if (!string.IsNullOrEmpty(context.CharacterConnection))
            score += 0.2;
        
        return Math.Min(1.0, score);
    }
}
