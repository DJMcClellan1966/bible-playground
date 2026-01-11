using AI_Bible_App.Core.Models;
using System.Text.Json;

namespace AI_Bible_App.Infrastructure.Repositories;

/// <summary>
/// Repository for managing user-created custom characters stored in JSON
/// </summary>
public class CustomCharacterRepository : ICustomCharacterRepository
{
    private readonly string _filePath;
    private List<CustomCharacter> _characters = new();
    private bool _loaded = false;
    private readonly object _lock = new();

    public CustomCharacterRepository()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "AI-Bible-App");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "custom_characters.json");
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        lock (_lock)
        {
            if (_loaded) return;
            _loaded = true;
        }

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _characters = JsonSerializer.Deserialize<List<CustomCharacter>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<CustomCharacter>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CustomCharacters] Failed to load: {ex.Message}");
                _characters = new List<CustomCharacter>();
            }
        }
    }

    private async Task SaveToFileAsync()
    {
        var json = JsonSerializer.Serialize(_characters, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task<List<CustomCharacter>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _characters.ToList();
    }

    public async Task<CustomCharacter?> GetByIdAsync(string id)
    {
        await EnsureLoadedAsync();
        return _characters.FirstOrDefault(c => c.Id == id);
    }

    public async Task SaveAsync(CustomCharacter character)
    {
        await EnsureLoadedAsync();
        
        character.ModifiedAt = DateTime.UtcNow;
        
        var existing = _characters.FindIndex(c => c.Id == character.Id);
        if (existing >= 0)
        {
            _characters[existing] = character;
        }
        else
        {
            character.CreatedAt = DateTime.UtcNow;
            _characters.Add(character);
        }
        
        await SaveToFileAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await EnsureLoadedAsync();
        _characters.RemoveAll(c => c.Id == id);
        await SaveToFileAsync();
    }

    public async Task<string> ExportToJsonAsync()
    {
        await EnsureLoadedAsync();
        return JsonSerializer.Serialize(_characters, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public async Task ImportFromJsonAsync(string json)
    {
        var imported = JsonSerializer.Deserialize<List<CustomCharacter>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (imported != null)
        {
            await EnsureLoadedAsync();
            
            foreach (var character in imported)
            {
                // Generate new IDs to avoid conflicts
                character.Id = Guid.NewGuid().ToString();
                character.CreatedAt = DateTime.UtcNow;
                character.ModifiedAt = DateTime.UtcNow;
                _characters.Add(character);
            }
            
            await SaveToFileAsync();
        }
    }
}
