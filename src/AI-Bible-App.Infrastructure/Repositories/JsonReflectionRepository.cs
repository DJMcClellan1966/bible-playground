using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using System.Text.Json;

namespace AI_Bible_App.Infrastructure.Repositories;

/// <summary>
/// JSON file-based implementation of reflection repository with caching.
/// Uses local app data folder for user-specific storage.
/// </summary>
public class JsonReflectionRepository : IReflectionRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private List<Reflection>? _cachedReflections;

    public JsonReflectionRepository()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App");
        
        Directory.CreateDirectory(appDataPath);
        _filePath = Path.Combine(appDataPath, "reflections.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<Reflection>> GetAllReflectionsAsync()
    {
        await EnsureCacheLoadedAsync();
        return _cachedReflections!.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<List<Reflection>> GetAllForUserAsync(string userId)
    {
        await EnsureCacheLoadedAsync();
        return _cachedReflections!
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task<Reflection?> GetReflectionByIdAsync(string id)
    {
        await EnsureCacheLoadedAsync();
        return _cachedReflections!.FirstOrDefault(r => r.Id == id);
    }

    public async Task<List<Reflection>> GetReflectionsByTypeAsync(ReflectionType type)
    {
        await EnsureCacheLoadedAsync();
        return _cachedReflections!
            .Where(r => r.Type == type)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task<List<Reflection>> GetFavoriteReflectionsAsync()
    {
        await EnsureCacheLoadedAsync();
        return _cachedReflections!
            .Where(r => r.IsFavorite)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task<List<Reflection>> SearchReflectionsAsync(string searchTerm)
    {
        await EnsureCacheLoadedAsync();
        var term = searchTerm.ToLowerInvariant();
        
        return _cachedReflections!
            .Where(r => r.Title.ToLowerInvariant().Contains(term) ||
                        r.SavedContent.ToLowerInvariant().Contains(term) ||
                        r.PersonalNotes.ToLowerInvariant().Contains(term) ||
                        r.Tags.Any(t => t.ToLowerInvariant().Contains(term)))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task SaveReflectionAsync(Reflection reflection)
    {
        await EnsureCacheLoadedAsync();
        
        var existingIndex = _cachedReflections!.FindIndex(r => r.Id == reflection.Id);
        if (existingIndex >= 0)
        {
            reflection.UpdatedAt = DateTime.UtcNow;
            _cachedReflections[existingIndex] = reflection;
        }
        else
        {
            _cachedReflections.Add(reflection);
        }
        
        await SaveCacheAsync();
    }

    public async Task DeleteReflectionAsync(string id)
    {
        await EnsureCacheLoadedAsync();
        _cachedReflections!.RemoveAll(r => r.Id == id);
        await SaveCacheAsync();
    }

    public async Task SaveAsync(Reflection reflection)
    {
        // SaveAsync is an alias for SaveReflectionAsync for sync operations
        await SaveReflectionAsync(reflection);
    }

    private async Task EnsureCacheLoadedAsync()
    {
        if (_cachedReflections != null) return;

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _cachedReflections = JsonSerializer.Deserialize<List<Reflection>>(json, _jsonOptions) ?? new();
            }
            catch
            {
                _cachedReflections = new();
            }
        }
        else
        {
            _cachedReflections = new();
        }
    }

    private async Task SaveCacheAsync()
    {
        var json = JsonSerializer.Serialize(_cachedReflections, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
