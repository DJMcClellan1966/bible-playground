using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Repository for managing Bible reflection/journal entries
/// </summary>
public interface IReflectionRepository
{
    Task<List<Reflection>> GetAllReflectionsAsync();
    Task<List<Reflection>> GetAllForUserAsync(string userId);
    Task<Reflection?> GetReflectionByIdAsync(string id);
    Task<List<Reflection>> GetReflectionsByTypeAsync(ReflectionType type);
    Task<List<Reflection>> GetFavoriteReflectionsAsync();
    Task<List<Reflection>> SearchReflectionsAsync(string searchTerm);
    Task SaveReflectionAsync(Reflection reflection);
    Task SaveAsync(Reflection reflection);
    Task DeleteReflectionAsync(string id);
}
