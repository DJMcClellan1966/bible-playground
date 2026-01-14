using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Infrastructure.Services
{
    public class InMemoryLongTermMemoryService : ILongTermMemoryService
    {
        private readonly List<MemoryRecord> _store = new();

        public Task StoreAsync(string key, string content)
        {
            _store.Add(new MemoryRecord { Key = key, Content = content, Timestamp = DateTime.UtcNow });
            return Task.CompletedTask;
        }

        public Task<IEnumerable<MemoryRecord>> QueryAsync(string query, int max = 5)
        {
            var results = _store.Where(m => m.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(max);
            return Task.FromResult(results);
        }
    }
}
