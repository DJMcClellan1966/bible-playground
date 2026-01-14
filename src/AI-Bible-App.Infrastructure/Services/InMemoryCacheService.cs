using AI_Bible_App.Core.Interfaces;
namespace AI_Bible_App.Infrastructure.Services
{
    public class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, (string Value, DateTime? Expiry)> _store = new();

        public Task SetAsync(string key, string value, TimeSpan? ttl = null)
        {
            DateTime? expiry = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null;
            _store[key] = (value, expiry);
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key)
        {
            if (_store.TryGetValue(key, out var v))
            {
                if (v.Expiry.HasValue && v.Expiry.Value < DateTime.UtcNow)
                {
                    _store.Remove(key);
                    return Task.FromResult<string?>(null);
                }
                return Task.FromResult<string?>(v.Value);
            }
            return Task.FromResult<string?>(null);
        }

        public Task RemoveAsync(string key)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }
}
