using System.Text.Json;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Repositories;

/// <summary>
/// Repository for loading Young's Literal Translation verses from JSON
/// </summary>
public class YltBibleRepository : IBibleRepository
{
    private readonly ILogger<YltBibleRepository> _logger;
    private readonly string _dataPath;
    private List<BibleVerse>? _cachedVerses;

    public YltBibleRepository(IConfiguration configuration, ILogger<YltBibleRepository> logger)
    {
        _logger = logger;
        _dataPath = configuration["Bible:YltDataPath"] ?? Path.Combine("Data", "Bible", "youngs.json");
        
        _logger.LogInformation("YltBibleRepository initialized with data path: {DataPath}", _dataPath);
    }

    public async Task<List<BibleVerse>> LoadAllVersesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedVerses != null)
        {
            _logger.LogDebug("Returning cached YLT verses ({Count} verses)", _cachedVerses.Count);
            return _cachedVerses;
        }

        try
        {
            if (!File.Exists(_dataPath))
            {
                _logger.LogWarning("YLT Bible data file not found at {DataPath}.", _dataPath);
                return new List<BibleVerse>();
            }

            var json = await File.ReadAllTextAsync(_dataPath, cancellationToken);
            var verses = JsonSerializer.Deserialize<List<BibleVerse>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _cachedVerses = verses ?? new List<BibleVerse>();
            _logger.LogInformation("Loaded {Count} YLT verses from {DataPath}", _cachedVerses.Count, _dataPath);
            
            return _cachedVerses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading YLT verses from {DataPath}", _dataPath);
            throw;
        }
    }

    public async Task<List<BibleVerse>> GetVersesAsync(
        string book, 
        int chapter, 
        int? startVerse = null, 
        int? endVerse = null,
        CancellationToken cancellationToken = default)
    {
        var allVerses = await LoadAllVersesAsync(cancellationToken);
        
        var query = allVerses.Where(v => 
            v.Book.Equals(book, StringComparison.OrdinalIgnoreCase) && 
            v.Chapter == chapter);

        if (startVerse.HasValue)
        {
            query = query.Where(v => v.Verse >= startVerse.Value);
        }

        if (endVerse.HasValue)
        {
            query = query.Where(v => v.Verse <= endVerse.Value);
        }

        return query.OrderBy(v => v.Verse).ToList();
    }

    public async Task<List<BibleVerse>> SearchVersesAsync(
        string searchText, 
        CancellationToken cancellationToken = default)
    {
        var allVerses = await LoadAllVersesAsync(cancellationToken);
        
        return allVerses
            .Where(v => v.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
