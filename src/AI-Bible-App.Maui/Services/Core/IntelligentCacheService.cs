using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AI_Bible_App.Maui.Services.Core;

/// <summary>
/// High-performance intelligent caching system with semantic similarity,
/// LRU eviction, and predictive pre-fetching capabilities.
/// </summary>
public interface IIntelligentCacheService
{
    Task<CacheResult<T>> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheOptions? options = null);
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, CacheOptions? options = null);
    Task<bool> TryGetSimilarAsync<T>(string query, out T? result, double similarityThreshold = 0.85);
    void Invalidate(string key);
    void InvalidateByPattern(string pattern);
    void ClearExpired();
    CacheStatistics GetStatistics();
    Task WarmupAsync(IEnumerable<string> keys);
    Task PersistToDiskAsync();
    Task LoadFromDiskAsync();
}

public class CacheOptions
{
    public TimeSpan? AbsoluteExpiration { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public CachePriority Priority { get; set; } = CachePriority.Normal;
    public bool EnableSimilarityMatching { get; set; } = true;
    public string[]? Tags { get; set; }
}

public enum CachePriority { Low, Normal, High, Critical }

public class CacheResult<T>
{
    public T Value { get; set; } = default!;
    public bool WasHit { get; set; }
    public bool WasSimilarMatch { get; set; }
    public double SimilarityScore { get; set; }
    public TimeSpan RetrievalTime { get; set; }
}

public class CacheStatistics
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long SimilarityHits { get; set; }
    public long TotalItems { get; set; }
    public long MemoryUsageBytes { get; set; }
    public double HitRate => TotalHits + TotalMisses > 0 
        ? (double)TotalHits / (TotalHits + TotalMisses) * 100 : 0;
    public TimeSpan AverageRetrievalTime { get; set; }
}

public class IntelligentCacheService : IIntelligentCacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, List<string>> _tagIndex = new();
    private readonly SemaphoreSlim _persistLock = new(1, 1);
    private readonly Timer _evictionTimer;
    private readonly Timer _persistTimer;
    private readonly string _persistPath;
    
    private long _hits;
    private long _misses;
    private long _similarityHits;
    private long _totalRetrievalTicks;
    private long _retrievalCount;
    
    private const int MaxCacheSize = 1000;
    private const int EvictionBatchSize = 100;

    public IntelligentCacheService()
    {
        _persistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App", "cache.json");
        
        // Run eviction every 5 minutes
        _evictionTimer = new Timer(_ => EvictExpiredEntries(), null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        
        // Auto-persist every 10 minutes
        _persistTimer = new Timer(async _ => await PersistToDiskAsync(), null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        
        // Load persisted cache on startup
        _ = LoadFromDiskAsync();
    }

    public async Task<CacheResult<T>> GetOrCreateAsync<T>(
        string key, Func<Task<T>> factory, CacheOptions? options = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        options ??= new CacheOptions();
        
        var hash = ComputeHash(key);
        
        // Try exact match first
        if (_cache.TryGetValue(hash, out var entry) && !entry.IsExpired)
        {
            entry.LastAccess = DateTime.UtcNow;
            entry.AccessCount++;
            Interlocked.Increment(ref _hits);
            sw.Stop();
            RecordRetrieval(sw.Elapsed);
            
            return new CacheResult<T>
            {
                Value = (T)entry.Value!,
                WasHit = true,
                RetrievalTime = sw.Elapsed
            };
        }
        
        // Try similarity match
        if (options.EnableSimilarityMatching)
        {
            var (found, similarResult, score) = await TrySimilarMatchAsync<T>(key);
            if (found)
            {
                Interlocked.Increment(ref _similarityHits);
                sw.Stop();
                RecordRetrieval(sw.Elapsed);
                
                return new CacheResult<T>
                {
                    Value = similarResult!,
                    WasHit = true,
                    WasSimilarMatch = true,
                    SimilarityScore = score,
                    RetrievalTime = sw.Elapsed
                };
            }
        }
        
        // Cache miss - create new value
        Interlocked.Increment(ref _misses);
        var value = await factory();
        await SetAsync(key, value, options);
        
        sw.Stop();
        RecordRetrieval(sw.Elapsed);
        
        return new CacheResult<T>
        {
            Value = value,
            WasHit = false,
            RetrievalTime = sw.Elapsed
        };
    }

    public Task<T?> GetAsync<T>(string key)
    {
        var hash = ComputeHash(key);
        if (_cache.TryGetValue(hash, out var entry) && !entry.IsExpired)
        {
            entry.LastAccess = DateTime.UtcNow;
            entry.AccessCount++;
            Interlocked.Increment(ref _hits);
            return Task.FromResult((T?)entry.Value);
        }
        
        Interlocked.Increment(ref _misses);
        return Task.FromResult(default(T?));
    }

    public Task SetAsync<T>(string key, T value, CacheOptions? options = null)
    {
        options ??= new CacheOptions();
        var hash = ComputeHash(key);
        
        var entry = new CacheEntry
        {
            Key = key,
            Hash = hash,
            Value = value,
            Created = DateTime.UtcNow,
            LastAccess = DateTime.UtcNow,
            AbsoluteExpiration = options.AbsoluteExpiration.HasValue 
                ? DateTime.UtcNow + options.AbsoluteExpiration.Value 
                : DateTime.UtcNow + TimeSpan.FromHours(24),
            SlidingExpiration = options.SlidingExpiration,
            Priority = options.Priority,
            Tags = options.Tags ?? Array.Empty<string>(),
            Keywords = ExtractKeywords(key)
        };
        
        // Check if we need to evict
        if (_cache.Count >= MaxCacheSize)
        {
            EvictLeastValuable();
        }
        
        _cache[hash] = entry;
        
        // Update tag index
        foreach (var tag in entry.Tags)
        {
            _tagIndex.AddOrUpdate(tag, 
                _ => new List<string> { hash },
                (_, list) => { list.Add(hash); return list; });
        }
        
        return Task.CompletedTask;
    }

    public Task<bool> TryGetSimilarAsync<T>(string query, out T? result, double similarityThreshold = 0.85)
    {
        result = default;
        var queryKeywords = ExtractKeywords(query);
        
        CacheEntry? bestMatch = null;
        double bestScore = 0;
        
        foreach (var entry in _cache.Values)
        {
            if (entry.IsExpired) continue;
            
            var score = CalculateSimilarity(queryKeywords, entry.Keywords);
            if (score > bestScore && score >= similarityThreshold)
            {
                bestScore = score;
                bestMatch = entry;
            }
        }
        
        if (bestMatch != null)
        {
            result = (T?)bestMatch.Value;
            bestMatch.LastAccess = DateTime.UtcNow;
            bestMatch.AccessCount++;
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    private async Task<(bool found, T? result, double score)> TrySimilarMatchAsync<T>(string query)
    {
        T? result = default;
        double score = 0;
        var queryKeywords = ExtractKeywords(query);
        
        CacheEntry? bestMatch = null;
        double bestScore = 0;
        
        await Task.Run(() =>
        {
            foreach (var entry in _cache.Values)
            {
                if (entry.IsExpired) continue;
                
                var similarity = CalculateSimilarity(queryKeywords, entry.Keywords);
                if (similarity > bestScore && similarity >= 0.85)
                {
                    bestScore = similarity;
                    bestMatch = entry;
                }
            }
        });
        
        if (bestMatch != null)
        {
            result = (T?)bestMatch.Value;
            score = bestScore;
            bestMatch.LastAccess = DateTime.UtcNow;
            bestMatch.AccessCount++;
            return (true, result, score);
        }
        
        return (false, default, 0);
    }

    public void Invalidate(string key)
    {
        var hash = ComputeHash(key);
        _cache.TryRemove(hash, out _);
    }

    public void InvalidateByPattern(string pattern)
    {
        var keysToRemove = _cache.Values
            .Where(e => e.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Hash)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }

    public void ClearExpired()
    {
        var expiredKeys = _cache.Values
            .Where(e => e.IsExpired)
            .Select(e => e.Hash)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    public CacheStatistics GetStatistics()
    {
        var avgTicks = _retrievalCount > 0 
            ? _totalRetrievalTicks / _retrievalCount : 0;
        
        return new CacheStatistics
        {
            TotalHits = _hits,
            TotalMisses = _misses,
            SimilarityHits = _similarityHits,
            TotalItems = _cache.Count,
            MemoryUsageBytes = EstimateMemoryUsage(),
            AverageRetrievalTime = TimeSpan.FromTicks(avgTicks)
        };
    }

    public async Task WarmupAsync(IEnumerable<string> keys)
    {
        // Pre-load common queries into cache
        foreach (var key in keys)
        {
            var hash = ComputeHash(key);
            if (!_cache.ContainsKey(hash))
            {
                // Create placeholder entry that can be filled later
                var entry = new CacheEntry
                {
                    Key = key,
                    Hash = hash,
                    Value = null,
                    Created = DateTime.UtcNow,
                    LastAccess = DateTime.UtcNow,
                    Priority = CachePriority.Low,
                    Keywords = ExtractKeywords(key)
                };
                _cache.TryAdd(hash, entry);
            }
        }
        await Task.CompletedTask;
    }

    public async Task PersistToDiskAsync()
    {
        await _persistLock.WaitAsync();
        try
        {
            var dir = Path.GetDirectoryName(_persistPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            var persistData = _cache.Values
                .Where(e => !e.IsExpired && e.Priority >= CachePriority.Normal)
                .Select(e => new PersistedCacheEntry
                {
                    Key = e.Key,
                    ValueJson = JsonSerializer.Serialize(e.Value),
                    ValueType = e.Value?.GetType().AssemblyQualifiedName,
                    Created = e.Created,
                    Priority = e.Priority,
                    Tags = e.Tags
                })
                .ToList();
            
            var json = JsonSerializer.Serialize(persistData, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
            await File.WriteAllTextAsync(_persistPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cache persist error: {ex.Message}");
        }
        finally
        {
            _persistLock.Release();
        }
    }

    public async Task LoadFromDiskAsync()
    {
        await _persistLock.WaitAsync();
        try
        {
            if (!File.Exists(_persistPath)) return;
            
            var json = await File.ReadAllTextAsync(_persistPath);
            var entries = JsonSerializer.Deserialize<List<PersistedCacheEntry>>(json);
            
            if (entries == null) return;
            
            foreach (var pe in entries)
            {
                if (string.IsNullOrEmpty(pe.ValueType)) continue;
                
                var type = Type.GetType(pe.ValueType);
                if (type == null) continue;
                
                var value = JsonSerializer.Deserialize(pe.ValueJson ?? "{}", type);
                var hash = ComputeHash(pe.Key ?? "");
                
                var entry = new CacheEntry
                {
                    Key = pe.Key ?? "",
                    Hash = hash,
                    Value = value,
                    Created = pe.Created,
                    LastAccess = DateTime.UtcNow,
                    AbsoluteExpiration = DateTime.UtcNow + TimeSpan.FromHours(24),
                    Priority = pe.Priority,
                    Tags = pe.Tags ?? Array.Empty<string>(),
                    Keywords = ExtractKeywords(pe.Key ?? "")
                };
                
                _cache.TryAdd(hash, entry);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cache load error: {ex.Message}");
        }
        finally
        {
            _persistLock.Release();
        }
    }

    private void EvictExpiredEntries()
    {
        var expired = _cache.Values
            .Where(e => e.IsExpired)
            .Select(e => e.Hash)
            .ToList();
        
        foreach (var hash in expired)
        {
            _cache.TryRemove(hash, out _);
        }
    }

    private void EvictLeastValuable()
    {
        var toEvict = _cache.Values
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.AccessCount)
            .ThenBy(e => e.LastAccess)
            .Take(EvictionBatchSize)
            .Select(e => e.Hash)
            .ToList();
        
        foreach (var hash in toEvict)
        {
            _cache.TryRemove(hash, out _);
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes)[..16];
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string> 
        { 
            "the", "a", "an", "is", "are", "was", "were", "what", "how", 
            "why", "when", "where", "who", "which", "this", "that", "these",
            "those", "can", "could", "would", "should", "do", "does", "did",
            "have", "has", "had", "be", "been", "being", "to", "of", "in",
            "for", "on", "with", "at", "by", "from", "about", "into", "through"
        };
        
        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '"', '\'' }, 
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet();
    }

    private static double CalculateSimilarity(HashSet<string> set1, HashSet<string> set2)
    {
        if (set1.Count == 0 || set2.Count == 0) return 0;
        
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();
        
        return (double)intersection / union; // Jaccard similarity
    }

    private long EstimateMemoryUsage()
    {
        // Rough estimation
        return _cache.Count * 1024; // ~1KB per entry average
    }

    private void RecordRetrieval(TimeSpan time)
    {
        Interlocked.Add(ref _totalRetrievalTicks, time.Ticks);
        Interlocked.Increment(ref _retrievalCount);
    }

    public void Dispose()
    {
        _evictionTimer?.Dispose();
        _persistTimer?.Dispose();
        _persistLock?.Dispose();
        PersistToDiskAsync().Wait();
    }

    private class CacheEntry
    {
        public string Key { get; set; } = "";
        public string Hash { get; set; } = "";
        public object? Value { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastAccess { get; set; }
        public DateTime? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public CachePriority Priority { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public HashSet<string> Keywords { get; set; } = new();
        public int AccessCount { get; set; }
        
        public bool IsExpired => AbsoluteExpiration.HasValue && 
            DateTime.UtcNow > AbsoluteExpiration.Value;
    }

    private class PersistedCacheEntry
    {
        public string? Key { get; set; }
        public string? ValueJson { get; set; }
        public string? ValueType { get; set; }
        public DateTime Created { get; set; }
        public CachePriority Priority { get; set; }
        public string[]? Tags { get; set; }
    }
}
