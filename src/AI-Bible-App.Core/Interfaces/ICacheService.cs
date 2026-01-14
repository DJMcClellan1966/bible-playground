namespace AI_Bible_App.Core.Interfaces
{
    public interface ICacheService
    {
        Task SetAsync(string key, string value, TimeSpan? ttl = null);
        Task<string?> GetAsync(string key);
        Task RemoveAsync(string key);
    }
}
