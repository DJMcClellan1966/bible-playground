using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Service for indexing and quickly searching Bible verses
/// </summary>
public interface IBibleVerseIndexService
{
    Task InitializeAsync();
    Task InitializeWithVersesAsync(IEnumerable<BibleVerse> verses);
    Task<IEnumerable<VerseSearchResult>> SearchVersesAsync(string query, int maxResults = 20);
    Task<string?> GetVerseTextAsync(string reference);
    bool IsInitialized { get; }
    int TotalVersesIndexed { get; }
}

public class VerseSearchResult
{
    public string Reference { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Relevance { get; set; }
}

public class BibleVerseIndexService : IBibleVerseIndexService
{
    private readonly ConcurrentDictionary<string, string> _verseIndex = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _wordIndex = new();
    private readonly ILogger<BibleVerseIndexService>? _logger;
    private bool _isInitialized;
    private int _totalVersesIndexed;

    public bool IsInitialized => _isInitialized;
    public int TotalVersesIndexed => _totalVersesIndexed;

    public BibleVerseIndexService(ILogger<BibleVerseIndexService>? logger = null)
    {
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        // No-op when called without verses - the MAUI layer should call InitializeWithVersesAsync
        if (_isInitialized) return Task.CompletedTask;
        
        _logger?.LogDebug("[BibleIndex] InitializeAsync called - waiting for verses from MAUI layer");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Initialize the index with pre-loaded Bible verses (called from MAUI layer)
    /// </summary>
    public async Task InitializeWithVersesAsync(IEnumerable<BibleVerse> verses)
    {
        if (_isInitialized) return;

        try
        {
            _logger?.LogInformation("[BibleIndex] Starting Bible verse indexing...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Index all verses in parallel for speed
            await Task.Run(() =>
            {
                Parallel.ForEach(verses, verse =>
                {
                    IndexVerse(verse.Reference, verse.Text);
                });
            });

            sw.Stop();
            _totalVersesIndexed = _verseIndex.Count;
            _isInitialized = true;
            
            _logger?.LogInformation($"[BibleIndex] Completed indexing {_totalVersesIndexed} verses in {sw.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"[BibleIndex] Completed indexing {_totalVersesIndexed} verses in {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[BibleIndex] Failed to initialize Bible index");
            System.Diagnostics.Debug.WriteLine($"[BibleIndex] Failed to initialize: {ex.Message}");
            _isInitialized = true; // Mark as initialized to prevent repeated failures
        }
    }

    public async Task<IEnumerable<VerseSearchResult>> SearchVersesAsync(string query, int maxResults = 20)
    {
        if (!_isInitialized)
            await InitializeAsync();

        return await Task.Run(() =>
        {
            var results = new List<VerseSearchResult>();
            var queryWords = NormalizeAndSplit(query);
            if (queryWords.Count == 0)
                return results;

            var candidateCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var word in queryWords)
            {
                if (!_wordIndex.TryGetValue(word, out var refs))
                    continue;

                foreach (var reference in refs)
                {
                    candidateCounts.TryGetValue(reference, out var count);
                    candidateCounts[reference] = count + 1;
                }
            }

            foreach (var (reference, matchCount) in candidateCounts)
            {
                if (!_verseIndex.TryGetValue(reference, out var text))
                    continue;

                var relevance = (double)matchCount / queryWords.Count;
                results.Add(new VerseSearchResult
                {
                    Reference = reference,
                    Text = text,
                    Relevance = relevance
                });
            }

            return results
                .OrderByDescending(r => r.Relevance)
                .Take(maxResults)
                .ToList();
        });
    }

    public async Task<string?> GetVerseTextAsync(string reference)
    {
        if (!_isInitialized)
            await InitializeAsync();

        var normalizedRef = NormalizeReference(reference);
        _verseIndex.TryGetValue(normalizedRef, out var text);
        return text;
    }

    private HashSet<string> NormalizeAndSplit(string text)
    {
        var normalized = text.ToLowerInvariant();
        var words = Regex.Split(normalized, @"\W+")
            .Where(w => w.Length > 2)
            .ToHashSet();
        return words;
    }

    private string NormalizeReference(string reference)
    {
        // Normalize verse references to a standard format
        // e.g., "John 3:16" -> "john_3_16"
        return Regex.Replace(reference.ToLowerInvariant(), @"[^\w]+", "_");
    }

    /// <summary>
    /// Add a verse to the index (for future population from Bible API)
    /// </summary>
    public void IndexVerse(string reference, string text)
    {
        var normalizedRef = NormalizeReference(reference);
        _verseIndex[normalizedRef] = text;

        // Index words for faster searching (thread-safe)
        var words = NormalizeAndSplit(text);
        foreach (var word in words)
        {
            var bag = _wordIndex.GetOrAdd(word, _ => new ConcurrentBag<string>());
            bag.Add(normalizedRef);
        }
    }
}
